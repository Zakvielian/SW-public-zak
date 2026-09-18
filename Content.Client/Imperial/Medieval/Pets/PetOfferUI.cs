using Content.Shared.Imperial.Medieval.Dogs;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Medieval.Dogs;

public sealed class PetOfferUI : BoundUserInterface
{
    private PetOffer? _window;

    public PetOfferUI(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new PetOffer();
        _window.OnClose += Close;

        _window.OnApplyPressed += () =>
        {
            SendMessage(new PetOfferMessage(true));
        };

        _window.OnRejectPressed += () =>
        {
            SendMessage(new PetOfferMessage(false));
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
