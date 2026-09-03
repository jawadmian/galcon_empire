# Testing & Quality Assurance Guide — GdUnit4 Standard

This guide details how to write, execute, and maintain automated tests in **Galcon Empire** using the **GdUnit4** testing framework.

---

## 🎯 Framework Standard: GdUnit4

All unit, integration, and regression testing in Galcon Empire must be written using **GdUnit4** for Godot 4 C#.

### Project Prerequisites:
1. **NuGet Dependency**: `.csproj` includes the official API:
   ```xml
   <ItemGroup>
     <PackageReference Include="gdunit4.api" Version="5.0.0" />
   </ItemGroup>
   ```
2. **Editor Plugin**: Enabled in `project.godot`:
   ```ini
   [editor_plugins]
   enabled=PackedStringArray("res://addons/gdUnit4/plugin.cfg", ...)
   ```

---

## 🚀 Test Execution Commands

### Step 1: Compile C# Project
```powershell
dotnet build
```
Verify the build succeeds with **0 errors and 0 warnings**.

### Step 2: Run GdUnit4 Test Suite via CLI
```powershell
.\addons\gdUnit4\runtest.cmd --godot_binary "E:\Tools\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe" -a res://Tests/
```

To run an individual test suite:
```powershell
.\addons\gdUnit4\runtest.cmd --godot_binary "E:\Tools\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe" -a res://Tests/MyTestSuite.cs
```

*(Optional fallback for standalone test scenes: `& "E:\Tools\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe" --headless res://Tests/EmpireTests.tscn --quit`)*

---

## ✍️ Writing GdUnit4 C# Tests

### Required Structure & Attributes
- Test classes must be decorated with `[TestSuite]`.
- Test methods must be decorated with `[TestCase]`.
- Assertions should use static imports from `GdUnit4.Assertions`:
  ```csharp
  using GdUnit4;
  using static GdUnit4.Assertions;
  ```

### Modern Assertion Standards
Always use current GdUnit4 assertion methods. Never use legacy or deprecated syntax:
| Recommended Method | Anti-Pattern (Deprecated / Avoid) |
| :--- | :--- |
| `AssertThat(obj).IsNotNull()` | `IsNotNil()`, `Assert(obj != null)` |
| `AssertThat(obj).IsNull()` | `IsNil()` |
| `AssertThat(val).IsEqual(expected)` | `Equals()` |
| `AssertThat(condition).IsTrue()` | `Assert(condition)` |
| `AssertThat(condition).IsFalse()` | `Assert(!condition)` |
| `AssertThat(collection).IsNotEmpty()` | `NotEmpty()` |
| `AssertThat(collection).IsEmpty()` | `Empty()` |
| `AssertThat(number).IsGreater(val)` | `Assert(number > val)` |

---

## 📋 GdUnit4 Test Suite Template

```csharp
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public partial class EmpireSimulationTest
{
    private Empire _testEmpire;
    private Star _testStar;

    [BeforeTest]
    public void Setup()
    {
        _testEmpire = new Empire { EmpireName = "Terran Ascendancy" };
        _testStar = new Star { StarName = "Sol" };
        _testEmpire.HomeStar = _testStar;
        _testEmpire._Ready();
    }

    [AfterTest]
    public void TearDown()
    {
        if (GodotObject.IsInstanceValid(_testEmpire) && !_testEmpire.IsQueuedForDeletion())
        {
            _testEmpire.Free();
        }
        if (GodotObject.IsInstanceValid(_testStar) && !_testStar.IsQueuedForDeletion())
        {
            _testStar.Free();
        }
    }

    [TestCase]
    public void TestStockpileInitializesToZero()
    {
        AssertThat(_testEmpire.ResourceStockpiles[ResourceType.Food]).IsEqual(0);
        AssertThat(_testEmpire.ResourceStockpiles[ResourceType.Ore]).IsEqual(0);
        AssertThat(_testEmpire.ResourceStockpiles[ResourceType.Money]).IsEqual(0);
    }

    [TestCase]
    public void TestConstructionDeductsResourcesWhenQueued()
    {
        var improvement = new ImprovementResource
        {
            ImprovementName = "Hydroponics Farm",
            BuildTime = 2,
            BuildCost = new Godot.Collections.Dictionary<ResourceType, int>
            {
                { ResourceType.Ore, 50 }
            }
        };

        // Grant ore to stockpile
        _testEmpire.ResourceStockpiles[ResourceType.Ore] = 100;

        bool started = _testEmpire.RequestConstruction(improvement, _testStar);

        AssertThat(started).IsTrue();
        AssertThat(_testEmpire.ResourceStockpiles[ResourceType.Ore]).IsEqual(50);
        
        improvement.Dispose();
    }
}
```

---

## 🛡️ Best Practices & Gotchas

1. **Lifecycle & Teardown**: Always clean up created nodes in `[AfterTest]` using `Free()` to prevent `ObjectDB` instance leaks.
2. **Defensive Autoload / Singleton Isolation**:
   - In unit tests, singletons like `EmpireManager.Instance`, `StarManager.Instance`, or `TickManager.Instance` may not be present unless spawned into the test tree.
   - Design simulation classes to function when dependencies are directly assigned (e.g. setting `empire.HomeStar = star`), allowing fast, isolated testing without booting the entire scene tree.
3. **Data Immutability**:
   - When instantiating `ImprovementResource` or custom resources in tests, do not share mutated resource instances across tests.
