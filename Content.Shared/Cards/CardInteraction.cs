using System.Linq;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Random.Helpers;
using Content.Shared.Verbs;

namespace Content.Shared.Cards;

public abstract partial class SharedCardSystem
{
    private void InitializeInteraction()
    {
        SubscribeLocalEvent<CardsComponent, ActivateInWorldEvent>(OnCardsActivate);
        SubscribeLocalEvent<CardsComponent, UseInHandEvent>(OnCardsUse);
        SubscribeLocalEvent<CardsComponent, GetVerbsEvent<AlternativeVerb>>(OnCardsAlternativeInteract);
    }

    // When 'E' pressed in the world
    private void OnCardsActivate(Entity<CardsComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        TryFlipCards(ent);
    }

    // When 'Z' pressed in hands
    // Will flip then fan then flip and fan
    private void OnCardsUse(Entity<CardsComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Fanned)
        {
            TryFanCards(ent);
            TryFlipCards(ent);
        }
        else if (ent.Comp.Flipped && ent.Comp.Cards.Count != 1)
        {
            TryFanCards(ent);
        }
        else
        {
            TryFlipCards(ent);
        }
    }

    private void OnCardsAlternativeInteract(Entity<CardsComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract || args.Hands == null)
            return;

        var user = args.User;

        if (TryGetFlipCardVerb(ent, user, out var flipVerb))
            args.Verbs.Add(flipVerb);

        if (TryGetShuffleCardVerb(ent, user, out var shuffleVerb))
            args.Verbs.Add(shuffleVerb);

        if (TryGetFanCardVerb(ent, user, out var fanVerb))
            args.Verbs.Add(fanVerb);

        if (!ent.Comp.Fanned || Hands.GetActiveItem(user) == ent.Owner)
            return;

        var priority = -200;
        if (ent.Comp.Flipped)
        {
            foreach (var card in ent.Comp.Cards)
            {
                if (TryGetTakeCardVerb(ent, user, card.CardInx, priority--, out var takeVerb))
                    args.Verbs.Add(takeVerb);
            }
        }
        else
        {
            if (TryGetTakeRandomCardVerb(ent, user, priority, out var takeVerb))
                args.Verbs.Add(takeVerb);
        }
    }

    public bool TryGetFlipCardVerb(Entity<CardsComponent> ent, EntityUid user, out AlternativeVerb verb)
    {
        verb = new AlternativeVerb
        {
            Text = Loc.GetString("comp-cards-flip"),
            Act = () => TryFlipCards(ent),
            Priority = -98,
        };
        return true;
    }

    public bool TryGetShuffleCardVerb(Entity<CardsComponent> ent, EntityUid user, out AlternativeVerb verb)
    {
        verb = new AlternativeVerb
        {
            Text = Loc.GetString("comp-cards-shuffle"),
            Act = () => TryShuffleCards(ent),
            Priority = -99,
        };
        return true;
    }

    public bool TryGetFanCardVerb(Entity<CardsComponent> ent, EntityUid user, out AlternativeVerb verb)
    {
        if (
            Container.TryGetContainingContainer(ent.Owner, out var container)
            && !Hands.EnumerateHands(container.Owner).Contains(container.ID)
        )
        {
            verb = null!;
            return false;
        }
        verb = new AlternativeVerb
        {
            Text = Loc.GetString("comp-cards-fan"),
            Act = () => TryFanCards(ent),
            Priority = -100,
        };
        return true;
    }

    public bool TryGetTakeCardVerb(
        Entity<CardsComponent> ent,
        EntityUid user,
        int cardInx,
        int? priority,
        out AlternativeVerb verb
    )
    {
        verb = null!;
        if (user == null)
            return false;
        if (!ent.Comp.Fanned && !ent.Comp.Flipped)
        {
            return false;
        }
        var card = GetCardFromInx(ent.Comp.Cards, cardInx);
        verb = new AlternativeVerb
        {
            Text = Loc.GetString(card.CardId.ToString().Replace('_', '-')),
            Act = () => TryTakeCard(ent, user, cardInx, out _),
            Category = VerbCategory.TakeCard,
            Priority = priority ?? -300,
        };
        return true;
    }

    public bool TryGetTakeRandomCardVerb(
        Entity<CardsComponent> ent,
        EntityUid user,
        int? priority,
        out AlternativeVerb verb
    )
    {
        if (!ent.Comp.Fanned)
        {
            verb = null!;
            return false;
        }
        var randomIndex = SharedRandomExtensions.PredictedRandom(Timing, GetNetEntity(ent)).Next(ent.Comp.Cards.Count);
        verb = new AlternativeVerb
        {
            Text = Loc.GetString("comp-cards-random-card"),
            Act = () => TryTakeCard(ent, user, randomIndex, out _),
            Priority = priority ?? -300,
        };
        return true;
    }
}
