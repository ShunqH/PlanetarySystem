using System;
using System.Collections.Generic;
using System.IO;
using PlanetSystem.Data;
using UnityEngine;

namespace PlanetSystem.Core
{
    /// <summary>
    /// The list shown in the Examples menu: built-in presets (read-only) followed by the user's saved cases.
    /// Saved cases are stored in one JSON file in Application.persistentDataPath, on macOS
    /// ~/Library/Application Support/&lt;Company&gt;/&lt;Product&gt;/saved_cases.json.
    /// </summary>
    public sealed class ScenarioLibrary
    {
        private readonly List<Scenario> _builtIns = new List<Scenario>(BuiltInScenarios.All);
        private readonly List<Scenario> _saved = new List<Scenario>();

        public static string FilePath => Path.Combine(Application.persistentDataPath, "saved_cases.json");

        public IReadOnlyList<Scenario> BuiltIns => _builtIns;
        public IReadOnlyList<Scenario> Saved => _saved;

        /// <summary>Raised after a case is saved or deleted.</summary>
        public event Action Changed;

        /// <summary>Problem found while reading the file at startup, shown once to the user; null if none.</summary>
        public string LoadProblem { get; private set; }

        public void Load()
        {
            _saved.Clear();
            LoadProblem = null;
            string path = FilePath;
            if (!File.Exists(path)) return;
            try
            {
                var warnings = new List<string>();
                _saved.AddRange(ScenarioJson.Deserialize(File.ReadAllText(path), warnings));
                foreach (var w in warnings) Debug.LogWarning(w);
                if (warnings.Count > 0) LoadProblem = $"{warnings.Count} saved case(s) could not be read and were skipped.";
            }
            catch (Exception e)
            {
                // Keep the unreadable file instead of overwriting it on the next save.
                string backup = path + $".unreadable-{DateTime.Now:yyyyMMdd-HHmmss}";
                try { File.Move(path, backup); } catch (Exception) { /* best effort */ }
                LoadProblem = $"The saved cases file could not be read ({e.Message}). It was moved to {Path.GetFileName(backup)}.";
                Debug.LogError(LoadProblem);
            }
        }

        public bool IsBuiltInName(string name)
        {
            foreach (var s in _builtIns) if (Scenario.NamesEqual(s.Name, name)) return true;
            return false;
        }

        public Scenario FindSaved(string name)
        {
            foreach (var s in _saved) if (Scenario.NamesEqual(s.Name, name)) return s;
            return null;
        }

        /// <summary>First free name of the form "Case N".</summary>
        public string SuggestName()
        {
            for (int n = 1; ; n++)
            {
                string name = $"Case {n}";
                if (FindSaved(name) == null && !IsBuiltInName(name)) return name;
            }
        }

        /// <summary>Adds the case, replacing a saved case with the same name. Returns an error message, or null.</summary>
        public string Save(Scenario scenario)
        {
            if (string.IsNullOrWhiteSpace(scenario.Name)) return "The name cannot be empty.";
            if (IsBuiltInName(scenario.Name)) return $"\"{scenario.Name}\" is the name of a built-in example.";
            var existing = FindSaved(scenario.Name);
            int index = existing != null ? _saved.IndexOf(existing) : -1;
            if (index >= 0) _saved[index] = scenario;
            else _saved.Add(scenario);

            string error = Write();
            if (error != null)
            {
                // Roll back the in-memory change so the list matches the file.
                if (index >= 0) _saved[index] = existing;
                else _saved.Remove(scenario);
                return error;
            }
            Changed?.Invoke();
            return null;
        }

        /// <summary>Deletes a saved case (built-ins cannot be deleted). Returns an error message, or null.</summary>
        public string Delete(string name)
        {
            var existing = FindSaved(name);
            if (existing == null) return IsBuiltInName(name) ? "Built-in examples cannot be deleted." : "Case not found.";
            int index = _saved.IndexOf(existing);
            _saved.RemoveAt(index);
            string error = Write();
            if (error != null)
            {
                _saved.Insert(index, existing);
                return error;
            }
            Changed?.Invoke();
            return null;
        }

        /// <summary>Writes the file atomically (temp file, then rename) so a crash never leaves it half written.</summary>
        private string Write()
        {
            try
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                File.WriteAllText(temp, ScenarioJson.Serialize(_saved));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"Saving cases failed: {e}");
                return $"Could not write the saved cases file: {e.Message}";
            }
        }
    }
}
