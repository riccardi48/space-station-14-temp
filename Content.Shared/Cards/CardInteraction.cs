using System.Linq;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Verbs;

namespace Content.Shared.Cards;

public abstract partial class SharedCardSystem
{
    private void OnCardsActivate(Entity<CardsComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        TryFlipCards(ent);
    }

    private void OnCardsUse(Entity<CardsComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Flipped && !ent.Comp.Fanned)
        {
            TryFanCards(ent);
        }
        else if (ent.Comp.Fanned)
        {
            TryFanCards(ent);
            TryFlipCards(ent);
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

        AlternativeVerb flip = new()
        {
            Text = Loc.GetString("comp-cards-flip"),
            Act = () => TryFlipCards(ent),
            Priority = -98,
        };
        args.Verbs.Add(flip);

        AlternativeVerb shuffle = new()
        {
            Text = Loc.GetString("comp-cards-shuffle"),
            Act = () => TryShuffleCards(ent),
            Priority = -99,
        };

        args.Verbs.Add(shuffle);

        if (
            (
                !Container.TryGetContainingContainer(ent.Owner, out var container)
                || Hands.EnumerateHands(container.Owner).ToList().Contains(container.ID)
            )
        )
        {
            AlternativeVerb fan = new()
            {
                Text = Loc.GetString("comp-cards-fan"),
                Act = () => TryFanCards(ent),
                Priority = -100,
            };

            args.Verbs.Add(fan);
        }

        if (ent.Comp.Fanned && Hands.GetActiveItem(user) != ent.Owner)
        {
            var priority = -200;
            for (var i = 0; i < ent.Comp.Cards.Count; i++)
            {
                var index = ent.Comp.Cards.Count - i - 1;
                var card = ent.Comp.Cards[index];
                var cardName = $"{card.CardId}";

                // Want this to have icon of the card
                // Not sure is possible
                AlternativeVerb take = new()
                {
                    Text = Loc.GetString(cardName.Replace('_', '-')),
                    Act = () => TryTakeCard(ent, user, index, out var _),
                    Category = VerbCategory.TakeCard,
                    Priority = priority,
                };

                priority--;

                args.Verbs.Add(take);
            }
        }
    }
}
