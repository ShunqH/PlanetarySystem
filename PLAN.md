# PlanetSystem — 可互动多体演化 Demo 总体方案

> Unity 6000.3.5f2 · URP 17.3 · 新版 Input System · uGUI
> 目标：面向行星系统新手 / 公众的 3D 可互动 N 体演示，重点解决高倾角轨道在 2D 里演示不清的问题。最终打包 macOS（后续可加 Windows）。

---

## 1. 核心设计原则

1. **物理与渲染彻底分离。** 积分器是纯 C#（不继承 MonoBehaviour），全程 `double` 精度，单位自成体系；Unity 只负责把状态"画出来"。这样能单测、能换积分器、也不受 Unity `float` 精度限制。
2. **两类天体。**
   - `Massive`：有质量，参与相互引力（恒星、大行星）。
   - `TestParticle`：零质量，只受 Massive 施力，彼此不互相作用，也不影响 Massive。
   Massive 之间是 O(N²)，Test 是 O(N·M)；Test 之间相互独立，后期可并行（Jobs/Burst）。
3. **物理单位：AU · yr · M☉**，G = 4π²（一年一 AU 的轨道正好周期 1）。这样默认时间步长、速度量级对人都直观，也方便 UI 直接显示。
4. **所有可调参数集中在一个 `SimulationSettings`（ScriptableObject）里**：时间步、每帧最大子步数、渲染缩放、轨迹长度等。演示时改数不改代码。

---

## 2. 物理核心（`Assets/Scripts/Physics/`，纯 C#）

| 文件 | 职责 |
|---|---|
| `Constants.cs` | G、单位换算（AU↔km、yr↔s、M☉↔kg、M⊕、MJ） |
| `Body.cs` | id、名字、类型、质量、半径、位置/速度（`double3` 自实现或 `System.Numerics`） |
| `OrbitalElements.cs` | (a, e, i, Ω, ω, M₀ 或 ν) ⇄ 状态矢量的双向转换；开普勒方程求解（牛顿迭代）。相对于指定"母体"（恒星、或行星→做卫星） |
| `IIntegrator.cs` | `Step(SimulationState, dt)` 接口 |
| `LeapfrogIntegrator.cs` | Kick-Drift-Kick 二阶辛积分，**默认**。能量长期不漂，最适合演示 |
| `Yoshida4Integrator.cs` | 四阶辛（Leapfrog 组合），精度更高，可选 |
| `RK4Integrator.cs` | 对照用，展示"非辛积分能量漂移" |
| `Gravity.cs` | 加速度计算，含软化参数 ε 防止近距离爆炸 |
| `SimulationState.cs` | 所有 Body 列表 + 当前时间 t + 累计能量/角动量诊断 |
| `Diagnostics.cs` | 总能量、总角动量、质心位置；相对误差随时间 |

时间推进：`Simulation.Advance(realDeltaTime)` 按"模拟年/真实秒"倍率累积应推进的时间，按固定 dt 走整数个子步，**每帧子步数设上限**避免卡死；剩余时间累积到下一帧。倍率、暂停、单步、反向（对辛积分直接 dt 取负即可）。

碰撞 / 弹出：距离小于半径和 → 合并（质量守恒动量守恒），或标记"碰撞"暂停；距离 > 设定边界 → 标记"弹出"。默认只提示不合并，作为选项。

**单元测试（Test Framework 已装）**：两体圆轨道周期 = 1 yr；开普勒椭圆 e=0.9 一圈后回到原点；元素→矢量→元素往返一致；能量相对误差随步长收敛阶数正确。

---

## 3. 场景与可视化（`Assets/Scripts/View/`）

- **`BodyView`**：每个 Body 一个球体（URP Lit/Unlit，自发光恒星）。半径用**对数/夸张缩放**并在 UI 里可切换"真实比例 / 可见比例"，否则地球在 1 AU 尺度下不可见。
- **渲染缩放**：1 AU = N Unity 单位（默认 10），可在 Settings 里改。位置用 `double` 计算后减去"焦点天体"位置再转 `float`，避免远距离抖动。
- **轨迹**：两种，可同时开
  1. **历史轨迹**：环形缓冲的 `LineRenderer`，可调长度（按时间）。
  2. **密切轨道椭圆**：从当前状态实时反解元素画一整圈，摄动下会看到椭圆自己在进动/翻转——这是展示高倾角、Kozai–Lidov 效应最直观的方式。
- **参考面**：黄道/不变平面网格 + 可选每个天体的**轨道面圆盘**（半透明）+ **升交点/近心点连线**、倾角弧标注。直接回答"倾角是什么"。
- **标签**：世界空间跟随的名字标签（uGUI Canvas 屏幕空间 + `WorldToScreenPoint`），可关。
- **选中高亮**：点击天体 → 高亮 + 右侧面板显示实时元素。

---

## 4. 镜头（`Assets/Scripts/Camera/`）

`OrbitCamera`（新版 Input System）：
- 鼠标左键拖拽 = 绕焦点旋转；右键/中键拖拽 = 平移；滚轮 = 缩放（对数）；触控板双指亦可。
- **焦点模式**：自由 / 锁定某天体（跟随）/ 质心。双击天体切焦点。
- **预设视角**：俯视（看轨道形状）、侧视（看倾角！）、沿某天体轨道法向看、45°。带平滑过渡。
- 可选"跟随参考系"：以某天体为原点画其他天体的相对运动（例如看卫星系统）。

---

## 5. UI（`Assets/Scripts/UI/`，uGUI + TextMeshPro）

分区：
1. **顶部时间栏**：播放/暂停、单步、倍率滑条（对数，0.01–1000 yr/s）、当前模拟时间、能量误差小字。
2. **左侧天体列表**：所有天体，类型图标，显示/隐藏、聚焦、删除。
3. **右侧属性面板**（选中天体或"添加天体"时）：
   - 基本：名字、类型（Massive/Test）、质量（单位可选 M☉/MJ/M⊕）、半径、颜色。
   - 轨道元素：母体下拉、a、e、i、Ω、ω、M₀（或 ν），滑条 + 数字框；改动时**实时预览**虚线椭圆，点"应用"才真正加进模拟（可以选"暂停时立即生效"）。
   - 也允许直接输入状态矢量（高级）。
4. **视图开关面板**：轨迹/椭圆/轨道面/参考网格/标签/真实比例。
5. **预设场景菜单**：太阳系（内/外）、热木星 + 高倾角伴星（Kozai–Lidov）、双星 + 环双星行星、共振链（TRAPPIST-1 风格）、随机盘。后续按你们的研究场景加。
6. 帮助浮层（快捷键、单位说明）。

（如你更倾向 UI Toolkit 也可以，但 uGUI 在运行时交互、拖拽、TMP 上更成熟稳定，先用它。）

---

## 6. 场景保存 / 载入（`Assets/Scripts/IO/`）

JSON（`JsonUtility` 或 Newtonsoft，后者对 double/列表更友好），内容 = 所有天体的初始条件 + 设置。内置预设放 `StreamingAssets/Scenarios/`，用户保存到 `Application.persistentDataPath`。可导出当前状态为 CSV 供 Python 后处理对照。

---

## 7. 工程结构

```
Assets/
  Scenes/Main.unity
  Scripts/
    Physics/        纯 C#，无 Unity 依赖（单独 asmdef，便于测试）
    Core/           Simulation 驱动 MonoBehaviour、Settings SO、事件
    View/           BodyView、Trail、OrbitEllipse、ReferencePlane、Labels
    Camera/         OrbitCamera、FocusController
    UI/             各面板
    IO/             Scenario 序列化
  Tests/EditMode/   物理单测
  Prefabs/ Materials/ Settings/ Resources/
  StreamingAssets/Scenarios/*.json
```

清理模板自带的 `TutorialInfo/`、`Readme.asset`；保留 Settings 里的 URP asset（用 PC_RPAsset）。

---

## 8. 分阶段实施

| 阶段 | 内容 | 验收 |
|---|---|---|
| **0 骨架** | 目录、asmdef、Settings SO、清理模板、Main 场景 | 编译通过 |
| **1 物理核心** | Body/State/Leapfrog/Gravity/元素转换 + 单测 | 两体测试通过，能量误差 < 1e-6/圈 |
| **2 最小可视** | 硬编码太阳+地球+木星+一颗高倾角 test，球体+历史轨迹+时间控制 | 能看到轨道在跑 |
| **3 镜头** | OrbitCamera、焦点切换、预设视角 | 能从侧面看倾角 |
| **4 轨道可视化增强** | 密切椭圆、轨道面圆盘、参考网格、标签、选中 | 摄动下椭圆进动可见 |
| **5 UI：添加/编辑天体** | 属性面板 + 元素预览 + 列表 | 公众能自己加行星 |
| **6 预设与存取** | 预设场景、JSON 存取、CSV 导出 | 一键切换场景 |
| **7 打磨与打包** | 帮助、性能（子步上限、Test 并行）、macOS 构建 | 组内可运行 |

每阶段你给具体细节需求，我逐步实现。

---

## 9. 打包注意（macOS）

- Build Profile：macOS，Architecture = **Apple Silicon + Intel (Universal)**。脚本后端 Mono 即可（IL2CPP 更慢编译，非必需）。
- 组内分发未签名 app 会被 Gatekeeper 拦：要么组员右键"打开"，要么用你的 Apple ID 做 ad-hoc 签名 `codesign --deep --force -s -`，正式发布再 notarize。
- Windows 版只需换目标平台，代码无平台相关部分。
- Input System 的触控板手势在 mac 上需注意滚轮增量尺度差异，做成可调灵敏度。

---

## 10. 已知风险 / 决策点

- **近距离交会**：固定步长辛积分在近距离会失真。演示用途先加软化 ε + 提示；如需研究级精度再加自适应步长或 Wisdom–Holman。
- **长时间演化**：模拟千万年级别时 double 累积误差与帧率都是问题；演示按"年/秒"倍率最高约 1000 即可。
- **Massive 数量**：O(N²) 在 N ≤ 50 完全无压力；Test 粒子 1000 级别可用 Jobs 并行。
- **UI 框架**：uGUI（推荐）vs UI Toolkit——待你确认。
- **JSON 库**：JsonUtility（零依赖，但不支持 Dictionary/多态）vs Newtonsoft（需装包）——预计用 Newtonsoft。
