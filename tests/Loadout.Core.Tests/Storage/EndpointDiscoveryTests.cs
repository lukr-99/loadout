using Loadout.Core.Storage;

namespace Loadout.Core.Tests.Storage;

public sealed class EndpointDiscoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "loadout-endpoint-" + Guid.NewGuid().ToString("N"));

    public EndpointDiscoveryTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Write(string name, string json)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void The_new_file_comes_first_and_the_old_one_follows()
    {
        var current = Write("new.json", """{ "url": "http://127.0.0.1:1", "pid": 10 }""");
        var old = Write("old.json", """{ "url": "http://127.0.0.1:2", "pid": 20 }""");

        var urls = EndpointDiscovery.ReadUrls([current, old], _ => true);

        Assert.Equal(["http://127.0.0.1:1", "http://127.0.0.1:2"], urls);
    }

    [Fact]
    public void A_file_whose_process_is_gone_is_skipped()
    {
        var current = Write("new.json", """{ "url": "http://127.0.0.1:1", "pid": 10 }""");
        var old = Write("old.json", """{ "url": "http://127.0.0.1:2", "pid": 20 }""");

        var urls = EndpointDiscovery.ReadUrls([current, old], pid => pid == 10);

        Assert.Equal(["http://127.0.0.1:1"], urls);
    }

    [Fact]
    public void A_dead_new_file_does_not_hide_a_live_old_one()
    {
        var current = Write("new.json", """{ "url": "http://127.0.0.1:1", "pid": 10 }""");
        var old = Write("old.json", """{ "url": "http://127.0.0.1:2", "pid": 20 }""");

        var urls = EndpointDiscovery.ReadUrls([current, old], pid => pid == 20);

        Assert.Equal(["http://127.0.0.1:2"], urls);
    }

    [Fact]
    public void Missing_broken_and_empty_files_are_ignored_and_the_same_url_is_listed_once()
    {
        var good = Write("a.json", """{ "url": "http://127.0.0.1:1", "pid": 10 }""");
        var same = Write("b.json", """{ "url": "http://127.0.0.1:1", "pid": 10 }""");
        var broken = Write("c.json", "{ not json");
        var noUrl = Write("d.json", """{ "pid": 10 }""");

        var urls = EndpointDiscovery.ReadUrls([Path.Combine(_root, "missing.json"), broken, noUrl, good, same], _ => true);

        Assert.Equal(["http://127.0.0.1:1"], urls);
    }

    [Fact]
    public void A_file_without_a_pid_is_kept()
    {
        var file = Write("a.json", """{ "url": "http://127.0.0.1:1" }""");

        Assert.Single(EndpointDiscovery.ReadUrls([file], _ => false));
    }
}
