using Loadout.Core.Startup;
using Loadout.Core.Windows;

namespace Loadout.Core.Tests.Startup;

public sealed class LegacyAutostartTests
{
    private sealed class FakeRegistration : IStartupRegistration
    {
        public bool Enabled { get; set; }
        public string? CommandLine { get; set; }
        public bool IsEnabled() => Enabled;
        public void Enable(string commandLine) { Enabled = true; CommandLine = commandLine; }
        public void Disable() => Enabled = false;
    }

    [Fact]
    public void An_enabled_old_entry_is_replaced_by_the_new_one()
    {
        var legacy = new FakeRegistration { Enabled = true };
        var current = new FakeRegistration();

        var changed = LegacyAutostart.Replace(legacy, current, "Loadout.exe");

        Assert.True(changed);
        Assert.False(legacy.Enabled);
        Assert.True(current.Enabled);
        Assert.Equal("Loadout.exe", current.CommandLine);
    }

    [Fact]
    public void An_old_entry_the_user_switched_off_stays_off()
    {
        var legacy = new FakeRegistration { Enabled = false };
        var current = new FakeRegistration();

        var changed = LegacyAutostart.Replace(legacy, current, "x");

        Assert.False(changed);
        Assert.False(current.Enabled);
    }

    [Fact]
    public void With_no_old_entry_the_current_one_is_left_alone()
    {
        var legacy = new FakeRegistration();
        var current = new FakeRegistration { Enabled = true, CommandLine = "keep" };

        Assert.False(LegacyAutostart.Replace(legacy, current, "x"));
        Assert.True(current.Enabled);
        Assert.Equal("keep", current.CommandLine);
    }
}
