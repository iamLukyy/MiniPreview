using MiniPreview.Hotkeys;
using Xunit;

namespace MiniPreview.Tests.Hotkeys;

public class HotkeyDefinitionTests
{
    [Fact]
    public void Parse_CtrlAltP_ReturnsCorrectFlags()
    {
        var def = HotkeyDefinition.Parse(new[] { "Ctrl", "Alt" }, "P");
        Assert.Equal(0x0001u | 0x0002u, def.Modifiers); // MOD_ALT | MOD_CONTROL
        Assert.Equal(0x50u, def.VirtualKey); // VK_P
    }

    [Fact]
    public void Parse_LowercaseModifiers_Works()
    {
        var def = HotkeyDefinition.Parse(new[] { "ctrl", "shift", "win" }, "f1");
        Assert.Equal(0x0002u | 0x0004u | 0x0008u, def.Modifiers);
        Assert.Equal(0x70u, def.VirtualKey); // VK_F1
    }

    [Fact]
    public void Parse_UnknownModifier_Throws()
    {
        Assert.Throws<ArgumentException>(() => HotkeyDefinition.Parse(new[] { "Meta" }, "P"));
    }

    [Fact]
    public void Parse_UnknownKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => HotkeyDefinition.Parse(new[] { "Ctrl" }, "GibberishKey"));
    }
}

[Trait("Category", "RequiresDesktop")]
public class HotkeyManagerIntegrationTests
{
    [Fact]
    public void Register_AndUnregister_UnusedCombo_Succeeds()
    {
        // Pouzij neobvyklou kombinaci aby nebyla kolize: Ctrl+Alt+Shift+F24
        var def = HotkeyDefinition.Parse(new[] { "Ctrl", "Alt", "Shift" }, "F24");

        // WPF HwndSource musi byt vytvoreny na STA threadu
        Exception? caught = null;
        var t = new Thread(() =>
        {
            try
            {
                using var mgr = new HotkeyManager();
                var id = mgr.Register(def, () => { });
                Assert.True(id > 0);
                mgr.Unregister(id);
            }
            catch (Exception ex) { caught = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (caught != null) throw caught;
    }
}
