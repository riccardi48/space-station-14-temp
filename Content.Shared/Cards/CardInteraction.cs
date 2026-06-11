using System.Linq;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Random.Helpers;
using Content.Shared.Stacks;
using Content.Shared.Verbs;

namespace Content.Shared.Cards;

public abstract partial class SharedCardSystem
{
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

        args.Verbs.Add(
            new AlternativeVerb
            {
                Text = Loc.GetString("comp-cards-flip"),
                Act = () => TryFlipCards(ent),
                Priority = -98,
            }
        );

        args.Verbs.Add(
            new AlternativeVerb
            {
                Text = Loc.GetString("comp-cards-shuffle"),
                Act = () => TryShuffleCards(ent),
                Priority = -99,
            }
        );

        // If the cards are in the current hand don't allow player to take from deck
        // Maybe bad?
        if (
            !Container.TryGetContainingContainer(ent.Owner, out var container)
            || Hands.EnumerateHands(container.Owner).Contains(container.ID)
        )
        {
            args.Verbs.Add(
                new AlternativeVerb
                {
                    Text = Loc.GetString("comp-cards-fan"),
                    Act = () => TryFanCards(ent),
                    Priority = -100,
                }
            );
        }

        // If deck is flipped, take a specific card from the deck
        // Otherwise take a random card from the deck
        if (ent.Comp.Fanned && Hands.GetActiveItem(user) != ent.Owner)
        {
            var priority = -200;
            if (ent.Comp.Flipped)
            {
                for (var i = 0; i < ent.Comp.Cards.Count; i++)
                {
                    var index = ent.Comp.Cards.Count - i - 1;
                    var card = ent.Comp.Cards[index];

                    args.Verbs.Add(
                        new AlternativeVerb
                        {
                            Text = Loc.GetString(card.CardId.ToString().Replace('_', '-')),
                            Act = () => TryTakeCard(ent, user, index, out _),
                            Category = VerbCategory.TakeCard,
                            Priority = priority--,
                        }
                    );
                }
            }
            else
            {
                var randomIndex = SharedRandomExtensions
                    .PredictedRandom(Timing, GetNetEntity(ent))
                    .Next(ent.Comp.Cards.Count);
                args.Verbs.Add(
                    new AlternativeVerb
                    {
                        Text = Loc.GetString("comp-cards-random-card"),
                        Act = () => TryTakeCard(ent, user, randomIndex, out _),
                        Priority = priority,
                    }
                );
            }
        }
    }
}
