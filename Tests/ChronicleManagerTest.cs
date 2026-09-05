using System.Collections.Generic;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

// Mock entity for testing Chronicle hyperlinking and tags
public class MockChronicleEntity : IChronicleEntity
{
    public string ChronicleId { get; set; }
    public string ChronicleName { get; set; }
    public string ChronicleType { get; set; }
    public Vector2? WorldPosition { get; set; }

    public MockChronicleEntity(string id, string name, string type, Vector2? pos = null)
    {
        ChronicleId = id;
        ChronicleName = name;
        ChronicleType = type;
        WorldPosition = pos;
    }
}

[TestSuite]
public partial class ChronicleManagerTest
{
    private ChronicleManager _manager;

    [BeforeTest]
    public void Setup()
    {
        _manager = new ChronicleManager();
        // Use an in-memory SQLite database for test isolation
        _manager.InitializeDatabase("Data Source=ChronicleTest;Mode=Memory;Cache=Shared");
        ChronicleManager.SetInstanceForTesting(_manager);
    }

    [AfterTest]
    public void TearDown()
    {
        _manager.CloseDatabase();
        ChronicleManager.SetInstanceForTesting(null);
        _manager.Dispose();
    }

    [TestCase]
    public void TestDatabaseInitializationAndEmptyQuery()
    {
        AssertThat(_manager).IsNotNull();
        var results = _manager.QueryEvents();
        AssertThat(results).IsNotNull();
        AssertThat(results.Count).IsEqual(0);
        AssertThat(_manager.GetEventCount()).IsEqual(0);
    }

    [TestCase]
    public void TestRecordAndRetrieveSingleEvent()
    {
        var record = new ChronicleRecord
        {
            Tick = 10,
            Category = EventCategory.Economic.ToString(),
            Title = "Factory Built",
            RawText = "Terran Ascendancy built a Factory on Sol.",
            FormattedBbcode = "[url=entity:empire:terran]Terran Ascendancy[/url] built a Factory on [url=entity:star:sol]Sol[/url].",
            PrimaryEntityId = "empire:terran",
            EntityTags = ",empire:terran,star:sol,",
            MetadataJson = "{\"cost\":100}"
        };

        _manager.RecordEvent(record);

        AssertThat(record.Id).IsGreater(0L);
        AssertThat(_manager.GetEventCount()).IsEqual(1);

        var events = _manager.QueryEvents();
        AssertThat(events.Count).IsEqual(1);
        AssertThat(events[0].Title).IsEqual("Factory Built");
        AssertThat(events[0].Tick).IsEqual(10);
        AssertThat(events[0].Category).IsEqual(EventCategory.Economic.ToString());
        AssertThat(events[0].PrimaryEntityId).IsEqual("empire:terran");
    }

    [TestCase]
    public void TestFilterByEntityTag()
    {
        var star1 = new MockChronicleEntity("sol", "Sol", "star");
        var star2 = new MockChronicleEntity("alpha_centauri", "Alpha Centauri", "star");
        var empire = new MockChronicleEntity("terran", "Terran Ascendancy", "empire");

        // Event on Sol
        _manager.RecordEvent(new ChronicleRecord
        {
            Tick = 5,
            Category = "Economic",
            Title = "Mine on Sol",
            RawText = "Mine built on Sol",
            FormattedBbcode = "Mine built on Sol",
            EntityTags = $",{star1.ChronicleId},{empire.ChronicleId},"
        });

        // Event on Alpha Centauri
        _manager.RecordEvent(new ChronicleRecord
        {
            Tick = 12,
            Category = "Economic",
            Title = "Mine on Alpha Centauri",
            RawText = "Mine built on Alpha Centauri",
            FormattedBbcode = "Mine built on Alpha Centauri",
            EntityTags = $",{star2.ChronicleId},{empire.ChronicleId},"
        });

        // Query by Sol entity
        var solFilter = new ChronicleFilter { EntityId = star1.ChronicleId };
        var solResults = _manager.QueryEvents(solFilter);
        AssertThat(solResults.Count).IsEqual(1);
        AssertThat(solResults[0].Title).IsEqual("Mine on Sol");

        // Query by Terran empire (should match both)
        var empireFilter = new ChronicleFilter { EntityId = empire.ChronicleId };
        var empireResults = _manager.QueryEvents(empireFilter);
        AssertThat(empireResults.Count).IsEqual(2);
    }

    [TestCase]
    public void TestFilterByCategoryAndTickRange()
    {
        _manager.RecordEvent(new ChronicleRecord
        {
            Tick = 10,
            Category = EventCategory.Political.ToString(),
            Title = "Election",
            RawText = "Election",
            FormattedBbcode = "Election",
            EntityTags = ""
        });

        _manager.RecordEvent(new ChronicleRecord
        {
            Tick = 20,
            Category = EventCategory.Military.ToString(),
            Title = "Battle",
            RawText = "Battle",
            FormattedBbcode = "Battle",
            EntityTags = ""
        });

        _manager.RecordEvent(new ChronicleRecord
        {
            Tick = 30,
            Category = EventCategory.Political.ToString(),
            Title = "Treaty Signed",
            RawText = "Treaty Signed",
            FormattedBbcode = "Treaty Signed",
            EntityTags = ""
        });

        // Filter by Category
        var polFilter = new ChronicleFilter { Category = EventCategory.Political };
        AssertThat(_manager.GetEventCount(polFilter)).IsEqual(2);

        // Filter by Tick Range (15 to 35)
        var tickFilter = new ChronicleFilter { MinTick = 15, MaxTick = 35 };
        var tickEvents = _manager.QueryEvents(tickFilter);
        AssertThat(tickEvents.Count).IsEqual(2);
        AssertThat(tickEvents[0].Tick).IsEqual(30); // Ordered DESC
        AssertThat(tickEvents[1].Tick).IsEqual(20);
    }

    [TestCase]
    public void TestPaginationWithLimitAndOffset()
    {
        for (int i = 1; i <= 10; i++)
        {
            _manager.RecordEvent(new ChronicleRecord
            {
                Tick = i,
                Category = "General",
                Title = $"Event #{i}",
                RawText = $"Event #{i}",
                FormattedBbcode = $"Event #{i}",
                EntityTags = ""
            });
        }

        AssertThat(_manager.GetEventCount()).IsEqual(10);

        // First page: limit 3, offset 0 -> Events #10, #9, #8
        var page1 = _manager.QueryEvents(new ChronicleFilter { Limit = 3, Offset = 0 });
        AssertThat(page1.Count).IsEqual(3);
        AssertThat(page1[0].Tick).IsEqual(10);
        AssertThat(page1[1].Tick).IsEqual(9);
        AssertThat(page1[2].Tick).IsEqual(8);

        // Second page: limit 3, offset 3 -> Events #7, #6, #5
        var page2 = _manager.QueryEvents(new ChronicleFilter { Limit = 3, Offset = 3 });
        AssertThat(page2.Count).IsEqual(3);
        AssertThat(page2[0].Tick).IsEqual(7);
        AssertThat(page2[1].Tick).IsEqual(6);
        AssertThat(page2[2].Tick).IsEqual(5);
    }

    [TestCase]
    public void TestChronicleEventBuilderTokenFormatting()
    {
        var sol = new MockChronicleEntity("sol", "Sol", "star", new Vector2(100, 200));
        var terran = new MockChronicleEntity("terran", "Terran Empire", "empire");

        var record = ChronicleEvent.Create(EventCategory.Economic, "Hydroponics Completed")
            .Involving(terran, isPrimary: true)
            .Involving(sol)
            .WithTemplate("{empire} constructed [b]{building}[/b] on {star}.")
            .WithArg("building", "Hydroponics Dome")
            .WithMetadata("cost", 50)
            .Record();

        AssertThat(record).IsNotNull();
        AssertThat(record.Title).IsEqual("Hydroponics Completed");
        AssertThat(record.PrimaryEntityId).IsEqual("terran");

        // Verify BBCode hyperlinks
        AssertThat(record.FormattedBbcode).IsEqual(
            "[url=entity:empire:terran]Terran Empire[/url] constructed [b]Hydroponics Dome[/b] on [url=entity:star:sol]Sol[/url]."
        );

        // Verify Raw text has no BBCode or brackets
        AssertThat(record.RawText).IsEqual(
            "Terran Empire constructed [b]Hydroponics Dome[/b] on Sol."
        );

        // Verify tags contain both entities
        AssertThat(record.EntityTags.Contains(",terran,")).IsTrue();
        AssertThat(record.EntityTags.Contains(",sol,")).IsTrue();
    }

    [TestCase]
    public void TestStarAndEmpireEntitiesIntegration()
    {
        var star = new Star { StarName = "Sol" };
        var empire = new Empire { EmpireName = "Terran Ascendancy", HomeStar = star };

        // Verify interface contract
        AssertThat(star.ChronicleId).IsEqual("star:sol");
        AssertThat(star.ChronicleType).IsEqual("star");
        AssertThat(star.ChronicleName).IsEqual("Sol");
        AssertThat(star.ToChronicleLink()).IsEqual("[url=entity:star:sol]Sol[/url]");

        AssertThat(empire.ChronicleId).IsEqual("empire:terran_ascendancy");
        AssertThat(empire.ChronicleType).IsEqual("empire");
        AssertThat(empire.ChronicleName).IsEqual("Terran Ascendancy");

        var improvement = new ImprovementResource
        {
            ImprovementName = "Hydroponics Farm",
            BuildTime = 1,
            BuildCost = new Godot.Collections.Dictionary<ResourceType, int>
            {
                { ResourceType.Food, 10 }
            }
        };

        // Give empire enough food to build
        empire.ResourceStockpiles[ResourceType.Food] = 50;

        // Queue improvement
        empire.QueueImprovement(improvement, star);

        // Update tick 1: Construction starts and finishes (BuildTime=1)
        empire.UpdateTick(1);

        // Query events from chronicle for this star
        var starEvents = _manager.QueryEvents(new ChronicleFilter { EntityId = star.ChronicleId });
        AssertThat(starEvents.Count).IsGreaterEqual(1);

        // Verify that Facility Completed was recorded
        bool foundFacilityEvent = false;
        foreach (var evt in starEvents)
        {
            if (evt.Title == "Facility Completed")
            {
                foundFacilityEvent = true;
                AssertThat(evt.FormattedBbcode.Contains("[b]Hydroponics Farm[/b]")).IsTrue();
            }
        }
        AssertThat(foundFacilityEvent).IsTrue();

        star.Dispose();
        empire.Dispose();
    }

    [TestCase]
    public void TestResetWorldStateClearsEventsAndResetsId()
    {
        _manager.RecordEvent(new ChronicleRecord
        {
            Tick = 1,
            Category = "Economic",
            Title = "First Event",
            RawText = "First event of previous game",
            FormattedBbcode = "First event of previous game",
            EntityTags = ""
        });

        AssertThat(_manager.GetEventCount()).IsEqual(1);

        // Reset world state for new game
        _manager.ResetWorldState();

        AssertThat(_manager.GetEventCount()).IsEqual(0);

        // Record new event in new game - ID should reset back to 1
        var newEvent = new ChronicleRecord
        {
            Tick = 0,
            Category = "Political",
            Title = "New Game Founded",
            RawText = "New Game Founded",
            FormattedBbcode = "New Game Founded",
            EntityTags = ""
        };
        _manager.RecordEvent(newEvent);

        AssertThat(_manager.GetEventCount()).IsEqual(1);
        AssertThat(newEvent.Id).IsEqual(1L);
    }
}
