using Loadout.Core.Startup;
using Loadout.Core.Windows;

namespace Loadout.Core.Tests.Startup;

public sealed class LegacyAutostartTests
{
    private sealed class FakeRegistration : IStartupRegistration
    {
        public bool Present { get; set; }
        public bool Enabled { get; set; }
        public string? CommandLine { get; set; }
        public bool ThrowOnEnable { get; set; }

        public bool HasEntry() => Present;
        public bool IsEnabled() => Present && Enabled;

        public void Enable(string commandLine)
        {
            if (ThrowOnEnable) throw new IOException("registry is locked");
            Present = true;
            Enabled = true;
            CommandLine = commandLine;
        }

        public void Disable()
        {
            Present = false;
            Enabled = false;
        }
    }

    private static FakeRegistration On() => new() { Present = true, Enabled = true };

    private static FakeRegistration SwitchedOff() => new() { Present = true, Enabled = false };

    [Fact]
    public void Replace_swaps_an_enabled_old_entry_for_the_new_one()
    {
        var legacy = On();
        var current = new FakeRegistration();

        Assert.True(LegacyAutostart.Replace(legacy, current, "Loadout.exe"));

        Assert.False(legacy.Present);
        Assert.True(current.Enabled);
        Assert.Equal("Loadout.exe", current.CommandLine);
    }

    [Fact]
    public void Replace_keeps_an_old_entry_the_user_switched_off_off()
    {
        var legacy = SwitchedOff();
        var current = new FakeRegistration();

        Assert.False(LegacyAutostart.Replace(legacy, current, "x"));

        Assert.False(legacy.Present);
        Assert.False(current.Present);
    }

    [Fact]
    public void Replace_with_no_old_entry_leaves_the_current_one_alone()
    {
        var legacy = new FakeRegistration();
        var current = new FakeRegistration { Present = true, Enabled = true, CommandLine = "keep" };

        Assert.False(LegacyAutostart.Replace(legacy, current, "x"));
        Assert.Equal("keep", current.CommandLine);
    }

    [Fact]
    public void Register_on_a_first_install_enables_the_new_entry()
    {
        var legacy = new FakeRegistration();
        var current = new FakeRegistration();

        Assert.True(LegacyAutostart.Register(legacy, current, "Loadout.exe"));
        Assert.True(current.Enabled);
    }

    [Fact]
    public void Register_over_an_enabled_old_entry_enables_the_new_one_and_removes_the_old()
    {
        var legacy = On();
        var current = new FakeRegistration();

        Assert.True(LegacyAutostart.Register(legacy, current, "Loadout.exe"));

        Assert.True(current.Enabled);
        Assert.False(legacy.Present);
    }

    [Fact]
    public void Register_keeps_the_users_off_choice_from_the_old_entry()
    {
        var legacy = SwitchedOff();
        var current = new FakeRegistration();

        Assert.False(LegacyAutostart.Register(legacy, current, "Loadout.exe"));

        Assert.False(current.Present);
        Assert.False(legacy.Present);
    }

    [Fact]
    public void A_failed_enable_leaves_the_old_entry_in_place()
    {
        var legacy = On();
        var current = new FakeRegistration { ThrowOnEnable = true };

        Assert.Throws<IOException>(() => LegacyAutostart.Register(legacy, current, "x"));
        Assert.True(legacy.Present);

        Assert.Throws<IOException>(() => LegacyAutostart.Replace(legacy, current, "x"));
        Assert.True(legacy.Present);
    }
}
