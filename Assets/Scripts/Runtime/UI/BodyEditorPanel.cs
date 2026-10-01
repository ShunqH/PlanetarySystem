using System.Collections.Generic;
using PlanetSystem.Core;
using PlanetSystem.Physics;
using PlanetSystem.View;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetSystem.UI
{
    /// <summary>
    /// Right-hand side panel used both to configure a new body (Add mode, with a live ghost preview)
    /// and to edit the selected body (Edit mode, changes apply immediately).
    /// Orbital elements follow Rebound: a, e, inc, Omega, omega and f (or M).
    /// </summary>
    public sealed class BodyEditorPanel : MonoBehaviour
    {
        private enum Mode { Hidden, Add, Edit }

        private SimulationController _controller;
        private SceneViewManager _scene;

        private RectTransform _root;
        private Text _title;
        private InputField _name;
        private GameObject _kindRow;
        private Dropdown _kind;
        private GameObject _massRow;
        private NumberField _mass;
        private Dropdown _massUnit;
        private int _massUnitIndex;
        private NumberField _radius;
        private Dropdown _radiusUnit;
        private int _radiusUnitIndex;
        private readonly List<Image> _swatches = new List<Image>();

        private GameObject _orbitGroup;
        private Dropdown _reference;
        private readonly List<int> _referenceIds = new List<int>();
        private NumberField _a, _e, _inc, _Omega, _omega, _anomaly;
        private Dropdown _anomalyType;
        private int _anomalyTypeIndex; // 0 = true anomaly f, 1 = mean anomaly M

        private Text _info;
        private Text _error;
        private Button _primary;
        private Button _delete;
        private Button _cancel;

        private BodyDefinition _def = new BodyDefinition();
        private Mode _mode = Mode.Hidden;
        private int _editId = -1;
        private bool _suppress;
        private float _nextLiveRefresh;
        private bool _applying;

        public bool IsAdding => _mode == Mode.Add;
        public bool IsEditing => _mode == Mode.Edit;

        public static BodyEditorPanel Create(Transform canvas, SimulationController controller, SceneViewManager scene)
        {
            var root = UIFactory.CreatePanel(canvas, "BodyEditorPanel", 360f);
            root.anchorMin = new Vector2(1f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 1f);
            root.anchoredPosition = new Vector2(-12f, -56f);

            var panel = root.gameObject.AddComponent<BodyEditorPanel>();
            panel._controller = controller;
            panel._scene = scene;
            panel._root = root;
            panel.Build();
            controller.RunStateChanged += panel.OnRunStateChanged;
            controller.BodiesChanged += panel.OnBodiesChanged;
            root.gameObject.SetActive(false);
            return panel;
        }

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        private void Build()
        {
            _title = UIFactory.CreateLabel(_root, "Add body", 17, FontStyle.Bold);

            // Name
            var nameRow = UIFactory.CreateRow(_root);
            UIFactory.CreateLabel(nameRow, "Name", 13, color: UIFactory.MutedColor, width: 120f);
            _name = UIFactory.CreateInputField(nameRow, "", s => { _def.Name = s; Apply(); });

            // Kind
            var kindRow = UIFactory.CreateRow(_root);
            _kindRow = kindRow.gameObject;
            UIFactory.CreateLabel(kindRow, "Type", 13, color: UIFactory.MutedColor, width: 120f);
            _kind = UIFactory.CreateDropdown(kindRow, new[] { "Massive (gravitating)", "Massless test particle" }, OnKindChanged);

            // Mass
            var massRow = UIFactory.CreateRow(_root);
            _massRow = massRow.gameObject;
            var massGroup = UIFactory.CreateGroup(massRow, "MassGroup", 0f);
            _mass = NumberField.Create(massGroup, "Mass", 0.001, 100.0, 1.0, logarithmic: true, format: "0.####", labelWidth: 120f);
            _mass.ValueChanged += v => { _def.Mass = v * Units.MassUnits[_massUnitIndex].InSolar; Apply(); };
            var massUnits = new List<string>();
            foreach (var u in Units.MassUnits) massUnits.Add(u.Label);
            _massUnit = UIFactory.CreateDropdown(massRow, massUnits, OnMassUnitChanged, width: 96f);

            // Radius
            var radiusRow = UIFactory.CreateRow(_root);
            var radiusGroup = UIFactory.CreateGroup(radiusRow, "RadiusGroup", 0f);
            _radius = NumberField.Create(radiusGroup, "Radius", 0.01, 100.0, 1.0, logarithmic: true, format: "0.####", labelWidth: 120f);
            _radius.ValueChanged += v => { _def.Radius = v * Units.RadiusUnits[_radiusUnitIndex].InAu; Apply(); };
            var radiusUnits = new List<string>();
            foreach (var u in Units.RadiusUnits) radiusUnits.Add(u.Label);
            _radiusUnit = UIFactory.CreateDropdown(radiusRow, radiusUnits, OnRadiusUnitChanged, width: 96f);

            // Color swatches
            var colorRow = UIFactory.CreateRow(_root);
            UIFactory.CreateLabel(colorRow, "Color", 13, color: UIFactory.MutedColor, width: 120f);
            for (int i = 0; i < Palette.BodyColors.Length; i++)
            {
                var c = Palette.BodyColors[i];
                var btn = UIFactory.CreateSwatch(colorRow, c, () => { _def.Color = c; RefreshSwatches(); Apply(); });
                _swatches.Add(btn.GetComponent<Image>());
            }

            // Orbit group
            var orbitGroup = UIFactory.CreateGroup(_root, "OrbitGroup");
            _orbitGroup = orbitGroup.gameObject;
            UIFactory.CreateSpacer(orbitGroup, 4f);
            UIFactory.CreateLabel(orbitGroup, "Orbit (Rebound convention)", 14, FontStyle.Bold);

            var refRow = UIFactory.CreateRow(orbitGroup);
            UIFactory.CreateLabel(refRow, "Primary", 13, color: UIFactory.MutedColor, width: 120f);
            _reference = UIFactory.CreateDropdown(refRow, new[] { "Center of mass" }, OnReferenceChanged);

            _a = NumberField.Create(orbitGroup, "a  semi-major axis (AU)", 0.01, 100.0, 1.0, logarithmic: true, format: "0.####");
            _a.ValueChanged += v => { _def.Elements.SemiMajorAxis = v; Apply(); };
            _e = NumberField.Create(orbitGroup, "e  eccentricity", 0.0, 0.99, 0.0, format: "0.###");
            _e.ValueChanged += v => { _def.Elements.Eccentricity = v; Apply(); };
            _inc = NumberField.Create(orbitGroup, "inc  inclination (°)", 0.0, 180.0, 0.0, format: "0.##");
            _inc.ValueChanged += v => { _def.Elements.Inclination = Constants.DegToRad(v); Apply(); };
            _Omega = NumberField.Create(orbitGroup, "Ω  asc. node (°)", 0.0, 360.0, 0.0, format: "0.##");
            _Omega.ValueChanged += v => { _def.Elements.LongitudeOfAscendingNode = Constants.DegToRad(v); Apply(); };
            _omega = NumberField.Create(orbitGroup, "ω  arg. pericenter (°)", 0.0, 360.0, 0.0, format: "0.##");
            _omega.ValueChanged += v => { _def.Elements.ArgumentOfPericenter = Constants.DegToRad(v); Apply(); };

            var anomalyRow = UIFactory.CreateRow(orbitGroup);
            var anomalyGroup = UIFactory.CreateGroup(anomalyRow, "AnomalyGroup", 0f);
            _anomaly = NumberField.Create(anomalyGroup, "f  true anomaly (°)", 0.0, 360.0, 0.0, format: "0.##");
            _anomaly.ValueChanged += OnAnomalyChanged;
            _anomalyType = UIFactory.CreateDropdown(anomalyRow, new[] { "f", "M" }, OnAnomalyTypeChanged, width: 52f);

            _info = UIFactory.CreateLabel(_root, "", 12, color: UIFactory.MutedColor, wrap: true);
            _error = UIFactory.CreateLabel(_root, "", 12, color: UIFactory.ErrorColor, wrap: true);

            var buttons = UIFactory.CreateRow(_root, 30f);
            _primary = UIFactory.CreateButton(buttons, "Add", OnPrimary);
            _delete = UIFactory.CreateButton(buttons, "Delete", OnDelete, UIFactory.DangerColor, width: 80f);
            _cancel = UIFactory.CreateButton(buttons, "Close", Close, UIFactory.NeutralButtonColor, width: 80f);
        }

        // ------------------------------------------------------------------
        // Open / close
        // ------------------------------------------------------------------

        public void OpenAdd()
        {
            _mode = Mode.Add;
            _editId = -1;
            _def = _controller.SuggestNewBody();
            _root.gameObject.SetActive(true);
            Populate();
            _scene.ShowPreview(_def);
        }

        public void OpenEdit(int id)
        {
            var rec = _controller.Find(id);
            if (rec == null) { Close(); return; }
            _scene.HidePreview();
            _mode = Mode.Edit;
            _editId = id;
            _def = _controller.ToDefinition(rec);
            _root.gameObject.SetActive(true);
            Populate();
            ApplyRunState();
        }

        /// <summary>Closes the panel only if it is in Add mode (used before actions that change the bodies).</summary>
        public void CloseAdd()
        {
            if (_mode == Mode.Add) Close();
        }

        // ------------------------------------------------------------------
        // Live view while integrating
        // ------------------------------------------------------------------

        /// <summary>
        /// Keeps the Edit panel in sync with changes made elsewhere (Reset, stop, removals).
        /// Changes made by this panel itself are ignored so unit choices are not reset while typing.
        /// </summary>
        private void OnBodiesChanged()
        {
            if (_applying || _mode != Mode.Edit) return;
            RefreshFromState();
        }

        private void OnRunStateChanged()
        {
            if (_controller.IsRunning && _mode == Mode.Add) Close();
            if (_mode == Mode.Edit) RefreshFromState();
        }

        /// <summary>Reloads the osculating elements of the edited body from the current state.</summary>
        private void RefreshFromState()
        {
            var rec = _controller.Find(_editId);
            if (rec == null) { Close(); return; }
            _def = _controller.ToDefinition(rec);
            Populate();
            ApplyRunState();
        }

        /// <summary>While integrating the panel is a read-only live display; only "Done" stays active.</summary>
        private void ApplyRunState()
        {
            bool readOnly = _controller.IsRunning;
            foreach (var s in _root.GetComponentsInChildren<Selectable>(true)) s.interactable = !readOnly;
            _primary.interactable = true;
            if (readOnly) _title.text = $"Live: {_def.Name}";
        }

        private void Update()
        {
            if (_mode != Mode.Edit || !_controller.IsRunning) return;
            if (Time.unscaledTime < _nextLiveRefresh) return;
            _nextLiveRefresh = Time.unscaledTime + 0.1f;
            RefreshFromState();
        }

        public void Close()
        {
            _mode = Mode.Hidden;
            _editId = -1;
            _scene.HidePreview();
            _root.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Widgets <- definition
        // ------------------------------------------------------------------

        private void Populate()
        {
            _suppress = true;

            bool add = _mode == Mode.Add;
            int othersCount = _controller.Bodies.Count - (add ? 0 : 1);
            bool hasPrimary = _controller.TryGetReferenceState(_def.ReferenceId, _editId, out _, out _, out _);

            _title.text = add ? (othersCount == 0 ? "Add star" : "Add body") : $"Edit: {_def.Name}";
            _name.SetTextWithoutNotify(_def.Name);

            _kindRow.SetActive(othersCount > 0);
            _kind.SetValueWithoutNotify(_def.Kind == BodyKind.Massive ? 0 : 1);
            _massRow.SetActive(_def.Kind == BodyKind.Massive);

            PickBestMassUnit();
            _massUnit.SetValueWithoutNotify(_massUnitIndex);
            ApplyMassUnit();
            PickBestRadiusUnit();
            _radiusUnit.SetValueWithoutNotify(_radiusUnitIndex);
            ApplyRadiusUnit();
            RefreshSwatches();

            _orbitGroup.SetActive(hasPrimary);
            if (hasPrimary)
            {
                RebuildReferenceOptions();
                var el = _def.Elements;
                _a.SetValue(el.SemiMajorAxis);
                _e.SetValue(el.Eccentricity);
                _inc.SetValue(Constants.RadToDeg(el.Inclination));
                _Omega.SetValue(Constants.RadToDeg(el.LongitudeOfAscendingNode));
                _omega.SetValue(Constants.RadToDeg(el.ArgumentOfPericenter));
                _anomalyType.SetValueWithoutNotify(_anomalyTypeIndex);
                RefreshAnomaly();
            }

            UIFactory.SetButtonText(_primary, add ? (othersCount == 0 ? "Add star" : "Add body") : "Done");
            _delete.gameObject.SetActive(!add);
            _cancel.gameObject.SetActive(add);
            _error.text = "";

            _suppress = false;
            UpdateInfo();
        }

        private void RebuildReferenceOptions()
        {
            _referenceIds.Clear();
            var labels = new List<string>();
            _referenceIds.Add(BodyDefinition.CenterOfMassReference);
            labels.Add("Center of mass (massive bodies)");
            foreach (var rec in _controller.Bodies)
            {
                if (!rec.Body.IsMassive || rec.Id == _editId) continue;
                _referenceIds.Add(rec.Id);
                labels.Add(rec.Name);
            }
            int idx = _referenceIds.IndexOf(_def.ReferenceId);
            if (idx < 0) { idx = 0; _def.ReferenceId = BodyDefinition.CenterOfMassReference; }
            UIFactory.SetOptions(_reference, labels, idx);
        }

        private void RefreshSwatches()
        {
            for (int i = 0; i < _swatches.Count; i++)
            {
                bool selected = ColorsClose(Palette.BodyColors[i], _def.Color);
                _swatches[i].transform.localScale = selected ? Vector3.one * 1.25f : Vector3.one;
            }
        }

        private static bool ColorsClose(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.02f;

        private void PickBestMassUnit()
        {
            // Choose the unit that displays the mass with a value >= 0.01, preferring solar masses for stars.
            _massUnitIndex = 0;
            for (int i = 0; i < Units.MassUnits.Length; i++)
            {
                if (_def.Mass / Units.MassUnits[i].InSolar >= 0.01) { _massUnitIndex = i; break; }
                _massUnitIndex = i;
            }
        }

        private void ApplyMassUnit()
        {
            var unit = Units.MassUnits[_massUnitIndex];
            double max = _massUnitIndex == 0 ? 100.0 : (_massUnitIndex == 1 ? 1000.0 : 10000.0);
            _mass.SetLabel($"Mass ({unit.Label.Split(' ')[0]})");
            _mass.SetRange(0.001, max, true);
            _mass.SetValue(_def.Mass / unit.InSolar);
        }

        private void PickBestRadiusUnit()
        {
            _radiusUnitIndex = 0;
            for (int i = 0; i < Units.RadiusUnits.Length; i++)
            {
                if (_def.Radius / Units.RadiusUnits[i].InAu >= 0.05) { _radiusUnitIndex = i; break; }
                _radiusUnitIndex = i;
            }
        }

        private void ApplyRadiusUnit()
        {
            var unit = Units.RadiusUnits[_radiusUnitIndex];
            double min = _radiusUnitIndex == 3 ? 100.0 : 0.01;
            double max = _radiusUnitIndex == 3 ? 1e7 : 100.0;
            _radius.SetLabel($"Radius ({unit.Label.Split(' ')[0]})");
            _radius.SetRange(min, max, true);
            _radius.SetValue(_def.Radius / unit.InAu);
        }

        private void RefreshAnomaly()
        {
            var el = _def.Elements;
            if (_anomalyTypeIndex == 0)
            {
                _anomaly.SetLabel("f  true anomaly (°)");
                _anomaly.SetValue(Constants.RadToDeg(el.TrueAnomaly));
            }
            else
            {
                _anomaly.SetLabel("M  mean anomaly (°)");
                _anomaly.SetValue(Constants.RadToDeg(el.MeanAnomaly));
            }
        }

        private void UpdateInfo()
        {
            if (_mode == Mode.Edit)
            {
                var rec = _controller.Find(_editId);
                if (rec != null && _controller.TryGetReferenceState(rec.ReferenceId, rec.Id, out _, out _, out _) &&
                    !_controller.TryGetElements(rec, out _, out _, out _))
                {
                    _info.text = "Currently unbound from its primary (e ≥ 1): the elements shown are not meaningful.";
                    return;
                }
            }
            if (!_controller.TryGetReferenceState(_def.ReferenceId, _editId, out _, out _, out var refMass))
            {
                _info.text = _mode == Mode.Add
                    ? "First body: placed at the origin. Add more bodies to define orbits."
                    : "This body is the only source of gravity, so it has no orbit of its own.";
                return;
            }
            double bodyMass = _def.Kind == BodyKind.Massive ? _def.Mass : 0.0;
            double mu = Constants.G * (refMass + bodyMass);
            var el = _def.Elements;
            double P = el.Period(mu);
            string periodText = P < 0.5 ? $"{P * Constants.DaysPerYear:0.##} days" : $"{P:0.###} yr";
            string text = $"Period {periodText}  ·  pericenter {el.Pericenter:0.####} AU  ·  apocenter {el.Apocenter:0.####} AU\n" +
                          $"Elements are relative to the primary (mass {refMass:0.####} M☉), as in Rebound.";
            if (_def.Kind == BodyKind.Massive && _def.ReferenceId == BodyDefinition.CenterOfMassReference)
            {
                double aBary = el.SemiMajorAxis * refMass / (refMass + bodyMass);
                text += $"\nDrawn ellipse is the barycentric orbit: a = {aBary:0.####} AU.";
            }
            if (_mode == Mode.Edit && _controller.IsRunning)
                text += "\nLive osculating elements. Stop the integration to edit.";
            else if (_mode == Mode.Edit && _controller.State.Time > 0.0)
                text += "\nEditing orbit or mass sets new initial conditions and resets t to 0.";
            _info.text = text;
        }

        // ------------------------------------------------------------------
        // Widgets -> definition
        // ------------------------------------------------------------------

        private void OnKindChanged(int index)
        {
            if (_suppress) return;
            var kind = index == 0 ? BodyKind.Massive : BodyKind.TestParticle;
            _def.Kind = kind;
            if (kind == BodyKind.Massive && _def.Mass <= 0.0) _def.Mass = Constants.JupiterMassInSolar;
            _massRow.SetActive(kind == BodyKind.Massive);
            _suppress = true;
            PickBestMassUnit();
            _massUnit.SetValueWithoutNotify(_massUnitIndex);
            ApplyMassUnit();
            _suppress = false;
            Apply();
        }

        private void OnMassUnitChanged(int index)
        {
            if (_suppress) return;
            _massUnitIndex = index;
            _suppress = true;
            ApplyMassUnit();
            _suppress = false;
        }

        private void OnRadiusUnitChanged(int index)
        {
            if (_suppress) return;
            _radiusUnitIndex = index;
            _suppress = true;
            ApplyRadiusUnit();
            _suppress = false;
        }

        private void OnReferenceChanged(int index)
        {
            if (_suppress) return;
            if (index < 0 || index >= _referenceIds.Count) return;
            _def.ReferenceId = _referenceIds[index];
            Apply();
        }

        private void OnAnomalyTypeChanged(int index)
        {
            if (_suppress) return;
            _anomalyTypeIndex = index;
            _suppress = true;
            RefreshAnomaly();
            _suppress = false;
        }

        private void OnAnomalyChanged(double degrees)
        {
            if (_suppress) return;
            double rad = Constants.DegToRad(degrees);
            if (_anomalyTypeIndex == 0) _def.Elements.TrueAnomaly = rad;
            else _def.Elements.MeanAnomaly = rad;
            Apply();
        }

        /// <summary>Pushes the current definition to the preview (Add) or to the simulation (Edit).</summary>
        private void Apply()
        {
            if (_suppress || _mode == Mode.Hidden) return;
            if (_mode == Mode.Add)
            {
                _scene.ShowPreview(_def);
                _error.text = "";
            }
            else
            {
                _applying = true;
                _error.text = _controller.UpdateBody(_editId, _def, out var err) ? "" : err;
                _applying = false;
            }
            UpdateInfo();
        }

        private void OnPrimary()
        {
            if (_mode == Mode.Add)
            {
                var rec = _controller.AddBody(_def, out var err);
                if (rec == null) { _error.text = err; return; }
                _scene.HidePreview();
                _controller.Select(rec.Id); // opens Edit mode through the selection event
            }
            else
            {
                Close();
                _controller.Select(-1);
            }
        }

        private void OnDelete()
        {
            if (_mode != Mode.Edit) return;
            int id = _editId;
            Close();
            _controller.RemoveBody(id);
        }
    }
}
