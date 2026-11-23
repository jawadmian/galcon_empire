using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public partial class BlackboardTests
{
    [TestCase]
    public void TestSetAndGet()
    {
        var bb = new Blackboard();
        bb.SetValue("TestInt", 42);
        bb.SetValue("TestBool", true);

        AssertThat(bb.GetValue<int>("TestInt")).IsEqual(42);
        AssertThat(bb.GetValue<bool>("TestBool")).IsTrue();
        AssertThat(bb.GetValue<int>("NonExistent", -1)).IsEqual(-1);
    }

    [TestCase]
    public void TestClone()
    {
        var bb = new Blackboard();
        bb.SetValue("Value", 100);

        var clone = bb.Clone();
        AssertThat(clone.GetValue<int>("Value")).IsEqual(100);

        clone.SetValue("Value", 200);
        AssertThat(bb.GetValue<int>("Value")).IsEqual(100);
        AssertThat(clone.GetValue<int>("Value")).IsEqual(200);
    }

    [TestCase]
    public void TestOrderIndependentHashAndEquality()
    {
        var a = new Blackboard();
        a.SetValue("Ore", 500L);
        a.SetValue("HasShipyard", true);

        var b = new Blackboard();
        b.SetValue("HasShipyard", true);
        b.SetValue("Ore", 500L);

        AssertThat(a.GetHashCode()).IsEqual(b.GetHashCode());
        AssertThat(a.Equals(b)).IsTrue();
    }
}
