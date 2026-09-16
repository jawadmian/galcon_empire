using Godot;
using System;

public partial class BlackboardTests : Node
{
    public override void _Ready()
    {
        GD.Print("--- Running Blackboard Tests ---");
        RunTest("Test Set and Get", TestSetAndGet);
        RunTest("Test Clone", TestClone);
        GD.Print("--- Blackboard Tests Completed ---");
    }

    private void RunTest(string name, Action testFunc)
    {
        try
        {
            testFunc();
            GD.Print($"[PASS] {name}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[FAIL] {name}: {ex.Message}");
        }
    }

    private void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private void TestSetAndGet()
    {
        var bb = new Blackboard();
        bb.SetValue("TestInt", 42);
        bb.SetValue("TestBool", true);

        Assert(bb.GetValue<int>("TestInt") == 42, "Int value should be 42.");
        Assert(bb.GetValue<bool>("TestBool") == true, "Bool value should be true.");
        Assert(bb.GetValue<int>("NonExistent", -1) == -1, "Should return default value if key doesn't exist.");
    }

    private void TestClone()
    {
        var bb = new Blackboard();
        bb.SetValue("Value", 100);

        var clone = bb.Clone();
        Assert(clone.GetValue<int>("Value") == 100, "Clone should have same values.");

        clone.SetValue("Value", 200);
        Assert(bb.GetValue<int>("Value") == 100, "Original should not be affected by clone modifications.");
        Assert(clone.GetValue<int>("Value") == 200, "Clone should be modified.");
    }
}
