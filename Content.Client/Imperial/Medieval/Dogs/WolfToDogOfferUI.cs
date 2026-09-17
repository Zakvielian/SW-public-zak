using Content.Shared.Imperial.Medieval.Dogs;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Medieval.Dogs;

public sealed class WolfToDogOfferUI : BoundUserInterface
{
    private WolfToDogOffer? _window;

    public WolfToDogOfferUI(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new WolfToDogOffer();
        _window.OnClose += Close;

        _window.OnApplyPressed += () =>
        {
            SendMessage(new WolfToDogOfferMessage(true));
        };

        _window.OnRejectPressed += () =>
        {
            SendMessage(new WolfToDogOfferMessage(false));
        };

        _window.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Close();
    }
}
