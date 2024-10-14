using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using Dawnsbury;
using Dawnsbury.Audio;
using Dawnsbury.Auxiliary;
using Dawnsbury.Core;
using Dawnsbury.Core.Mechanics.Rules;
using Dawnsbury.Core.Animations;
using Dawnsbury.Core.CharacterBuilder;
using Dawnsbury.Core.CharacterBuilder.AbilityScores;
using Dawnsbury.Core.CharacterBuilder.Feats;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Common;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.Spellbook;
using Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb;
using Dawnsbury.Core.CharacterBuilder.Selections.Options;
using Dawnsbury.Core.CharacterBuilder.Spellcasting;
using Dawnsbury.Core.CombatActions;
using Dawnsbury.Core.Coroutines;
using Dawnsbury.Core.Coroutines.Options;
using Dawnsbury.Core.Coroutines.Requests;
using Dawnsbury.Core.Creatures;
using Dawnsbury.Core.Creatures.Parts;
using Dawnsbury.Core.Intelligence;
using Dawnsbury.Core.Mechanics;
using Dawnsbury.Core.Mechanics.Core;
using Dawnsbury.Core.Mechanics.Enumerations;
using Dawnsbury.Core.Mechanics.Targeting;
using Dawnsbury.Core.Mechanics.Targeting.TargetingRequirements;
using Dawnsbury.Core.Mechanics.Targeting.Targets;
using Dawnsbury.Core.Mechanics.Treasure;
using Dawnsbury.Core.Possibilities;
using Dawnsbury.Core.Roller;
using Dawnsbury.Core.StatBlocks;
using Dawnsbury.Core.StatBlocks.Description;
using Dawnsbury.Core.Tiles;
using Dawnsbury.Display;
using Dawnsbury.Display.Illustrations;
using Dawnsbury.Display.Text;
using Dawnsbury.IO;
using Dawnsbury.Modding;
using Dawnsbury.ThirdParty.SteamApi;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static System.Collections.Specialized.BitVector32;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;
using System.Runtime.Intrinsics.Arm;
using System.Xml;
using Dawnsbury.Core.Mechanics.Damage;
using System.Runtime.CompilerServices;
using System.ComponentModel.Design;
using System.Text;
using static Dawnsbury.Core.CharacterBuilder.FeatsDb.TrueFeatDb.BarbarianFeatsDb.AnimalInstinctFeat;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Diagnostics.Metrics;
using Microsoft.Xna.Framework.Audio;
using static System.Reflection.Metadata.BlobBuilder;
using Dawnsbury.Core.CharacterBuilder.FeatsDb;
using Dawnsbury.Campaign.Encounters;
using Dawnsbury.Core.Animations.Movement;
using static Dawnsbury.Mods.Ancestries.Nagaji.ModEnums;

namespace Dawnsbury.Mods.Ancestries.Nagaji {

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    internal static class ModEnums {
        //public enum CreatureId {
        //    UNSEEN_GUARDIAN
        //}
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static class Loader {
        internal static string flavourText = "With a mix of humanoid and serpentine features, nagaji are heralds, companions, and servitors of powerful nagas. They hold a deep reverence for holy areas and spiritual truths, " +
            "an aspect many others find as intimidating as a nagaji's appearance.\n\nNagaji gain a 1d6 unarmed attack, determined by their heritage, with the finesse, unarmed and brawling traits.";

        // Traits
        internal static Trait tNagaji = ModManager.RegisterTrait("NagajiAncestryTrait", new TraitProperties("Nagaji", true) { IsAncestryTrait = true });

        // QEffects
        internal static QEffectId qfHypnoticLureUsed = ModManager.RegisterEnumMember<QEffectId>("HypnoticLureUsed");

        // Feats
        internal static FeatName ftHoodedNagaji = ModManager.RegisterFeatName("Hooded Nagaji");

        // Illustrations
        internal static ModdedIllustration illBlightBomb = new ModdedIllustration("NagajiAssets/BlightBomb.png");
        internal static ModdedIllustration illWhip = new ModdedIllustration("NagajiAssets/Whip.png");

        [DawnsburyDaysModMainMethod]
        public static void LoadMod() {
            AddFeats(CreateFeats());

            ModManager.RegisterNewItemIntoTheShop("blight bomb", itemName => {
                return new Item(itemName, illBlightBomb, "blight bomb (lesser)", 1, 3, new Trait[] { Trait.Alchemical, Trait.Bomb, Trait.Consumable, Trait.Poison, Trait.Splash, Trait.Thrown, Trait.Martial })
                .WithDescription("Blight bombs contain volatile toxic chemicals that rot flesh.")
                .WithWeaponProperties(new WeaponProperties("1d8", DamageKind.Poison) {  }
                    .WithRangeIncrement(4)
                    .WithAdditionalSplashDamage(1)
                    .WithAdditionalPersistentDamage("1", DamageKind.Poison)
                );
            });
        }

        private static void AddFeats(IEnumerable<Feat> feats) {
            foreach (Feat feat in feats) {
                ModManager.AddFeat(feat);
            }
        }

        private static IEnumerable<Feat> CreateFeats() {
            // Main ancestry
            yield return new AncestrySelectionFeat(ModManager.RegisterFeatName("Nagaji", "Nagaji"), flavourText, new List<Trait> { tNagaji, Trait.Humanoid }, 10, 5, new List<AbilityBoost>() { new EnforcedAbilityBoost(Ability.Strength), new FreeAbilityBoost() }, LoadHeritages().ToList());

            // Ancestry feats
            yield return new TrueFeat(ModManager.RegisterFeatName("Cold Minded"), 1, "The subtle strands of beguiling magic leave little impression on your mind.",
                "You gain a +1 circumstance bonus to saving throws against emotion effects, and whenever you roll a success on a saving throw against an emotion effect, you get a critical success instead.", new Trait[] { tNagaji }, null)
            .WithOnCreature((sheet, creature) => {
                creature.AddQEffect(new QEffect("Cold Mind", "+1 circumstance bonus and successes upgraded to critical success, against emotion effects.") {
                    BonusToDefenses = (self, action, defence) => {
                        if (action == null) {
                            return null;
                        }
                        if (action.HasTrait(Trait.Emotion)) {
                            return new Bonus(1, BonusType.Circumstance, "Cold Minded");
                        }
                        return null;
                    },
                    AdjustSavingThrowResult = (self, action, result) => {
                        if (action == null) {
                            return result;
                        }
                        if (action.HasTrait(Trait.Emotion) && result == CheckResult.Success) {
                            return CheckResult.CriticalSuccess;
                        }
                        return result;
                    }
                });
            });

            Feat nagajiSpellFamiliarity = new TrueFeat(ModManager.RegisterFeatName("Nagaji Spell Familiarity"), 1, "Either through study, exposure, or familial devotion, you have the magic of nagas bubbling in your blood.", "You can cast the Daze cantrip as an innate Occult spell, using your Charisma ability score.", new Trait[] { tNagaji }, null)
            .WithOnCreature(creature => {
                creature.GetOrCreateSpellcastingSource(SpellcastingKind.Innate, tNagaji, Ability.Charisma, Trait.Occult).WithSpells(new SpellId[1] { SpellId.Daze}, 1);
            });

            yield return nagajiSpellFamiliarity;

            yield return new TrueFeat(ModManager.RegisterFeatName("Water Nagaji"), 1, "Much like a water naga, you've formed a connection to a sacred or pristine body of water, either as a home or a place to protect.",
                "You gain a swim speed.", new Trait[] { tNagaji }, null)
            .WithOnCreature(creature => {
                creature.AddQEffect(QEffect.Swimming());
            });

            yield return new TrueFeat(ModManager.RegisterFeatName("Nagaji Venom Lore"), 1, "You've studied the closely guarded secrets of naga venom, allowing you to enhance poisons in your possession.",
                "The first bomb with the poison trait that you throw each day deals an additional die of damage, thanks to your modifications." +
                "\n\nIn addition, you are trained with bombs and for the purpose of determining your proficiency, bombs are simple weapons for you.", new Trait[] { tNagaji, Trait.Homebrew, Trait.Poison }, null)
            .WithOnSheet((Action<CalculatedCharacterSheetValues>) (sheet => {
                sheet.Proficiencies.Set(Trait.Bomb, Proficiency.Trained);
                sheet.Proficiencies.AddProficiencyAdjustment((Func<List<Trait>, bool>)(item => item.Contains(Trait.Bomb)), Trait.Simple);
            }))
            .WithOnCreature(creature => {
                creature.AddQEffect(new QEffect("Nagaji Venom Lore", "Bombs with the poison trait deal an additional damage die.") {
                    StartOfCombat = async self => {
                        if (self.Owner.PersistentUsedUpResources.UsedUpActions.Contains("Nagaji Venom Lore")) {
                            self.Name += " (Expended)";
                        }
                    },
                    IncreaseItemDamageDieCount = (self, item) => {
                        if (!self.Owner.PersistentUsedUpResources.UsedUpActions.Contains("Nagaji Venom Lore") && item.HasTrait(Trait.Bomb) && item.HasTrait(Trait.Poison)) {
                            return true;
                        }
                        return false;
                    },
                    AfterYouDealDamage = async (owner, action, target) => {
                        if (owner.PersistentUsedUpResources.UsedUpActions.Contains("Nagaji Venom Lore")) {
                            return;
                        }
                        if (action.Item != null && action.Item.WeaponProperties != null && action.Item.HasTrait(Trait.Bomb) && action.Item.HasTrait(Trait.Poison)) {
                            owner.PersistentUsedUpResources.UsedUpActions.Add("Nagaji Venom Lore");
                            owner.QEffects.FirstOrDefault(qf => qf.Name == "Nagaji Venom Lore").Name += " (Expended)";
                        }
                    }
                });
            });

            // Level 5
            //yield return new TrueFeat(ModManager.RegisterFeatName("Hypnotic Lure"), 5, "-", "-", new Trait[] { Trait.Concentrate, Trait.Enchantment, Trait.Mental, tNagaji, Trait.Occult, Trait.Visual }, null)
            //.WithOnCreature(creature => {
            //    creature.AddQEffect(new QEffect() {
            //        ProvideMainAction = self => {
            //            if (self.Owner.HasEffect(qfHypnoticLureUsed)) {
            //                return null;
            //            }

            //            return (ActionPossibility)new CombatAction(self.Owner, IllustrationName.Dominate, "Hypnotic Lure", new Trait[] { Trait.Concentrate, Trait.Enchantment, Trait.Mental, tNagaji, Trait.Occult, Trait.Visual },
            //                "",
            //                Target.Ranged(6))
            //            .WithSoundEffect(SfxName.SnakeHiss)
            //            .WithActionCost(2)
            //            .WithSavingThrow(new SavingThrow(Defense.Will, self.Owner.ClassOrSpellDC()))
            //            .WithEffectOnEachTarget(async (action, user, target, result) => {
            //                if (result == CheckResult.Success || result == CheckResult.CriticalSuccess) {
            //                    return;
            //                }

            //                int num = 1;

            //                if (result == CheckResult.CriticalFailure) {
            //                    num = 3;
            //                }

            //                target.AddQEffect(new QEffect("Hypnotic Lure", "This creature will approach you for the given number of actions on its next turn. It can't take reactions until then.", ExpirationCondition.ExpiresAtEndOfYourTurn, user, (Illustration)IllustrationName.Dominate) {
            //                    CannotExpireThisTurn = true,
            //                    Value = num,
            //                    Id = QEffectId.Commanded,
            //                    Tag = "APPROACH",
            //                    CountsAsADebuff = true,
            //                    StateCheck = (Action<QEffect>)(sc => sc.Owner.AddQEffect(new QEffect(ExpirationCondition.Ephemeral) {
            //                        Id = QEffectId.CannotTakeReactions
            //                    }))
            //                });
            //            })
            //            .WithEffectOnSelf(user => {
            //                user.AddQEffect(new QEffect() { Id = qfHypnoticLureUsed, ExpiresAt = ExpirationCondition.Never});
            //            });
            //        }
            //    });
            //});

            //yield return new TrueFeat(ModManager.RegisterFeatName("Skin Split"), 5, "-", "-", new Trait[] { tNagaji }, null)
            //.WithOnCreature(creature => {
            //    creature.AddQEffect(new QEffect() {
            //        ProvideMainAction = self => {
            //            if (self.Owner.PersistentUsedUpResources.UsedUpActions.Contains("Skin Split")) {
            //                return null;
            //            }

            //            return (ActionPossibility)new CombatAction(self.Owner, IllustrationName.Soothe, "Skin Split", new Trait[] { tNagaji },
            //                "-",
            //                Target.Self())
            //            .WithSoundEffect(SfxName.Fabric)
            //            .WithActionCost(2)
            //            .WithEffectOnSelf(user => {
            //                user.RemoveAllQEffects(qf => qf.Key == "PersistentDamage:" + DamageKind.Fire.ToString() || qf.Key == "PersistentDamage:" + DamageKind.Acid.ToString());
            //                user.PersistentUsedUpResources.UsedUpActions.Add("Skin Split");
            //            });
            //        }
            //    });
            //});

            //yield return new TrueFeat(ModManager.RegisterFeatName("Venom Spit"), 5, "-", "-", new Trait[] { tNagaji }, null)
            //.WithOnCreature(creature => {
            //    if (creature.HasFeat(ftHoodedNagaji)) {
            //        creature.AddQEffect(new QEffect() {
            //            YouHaveCriticalSpecialization = (self, item, action, target) => {
            //                if (item.Name == "Venomous Spit") {
            //                    int damage = item.WeaponProperties.DamageDieCount;
            //                    target.AddQEffect(QEffect.Dazzled());
            //                    return true;
            //                }
            //                return false;
            //            }
            //        });
            //        return;
            //    }

            //    creature.AddQEffect(new QEffect() {
            //        AdditionalUnarmedStrike = new Item(IllustrationName.AcidSplash, "Venomous Spit", new Trait[] { Trait.Unarmed, Trait.Ranged }).WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Poison).WithRangeIncrement(2)),
            //        YouHaveCriticalSpecialization = (self, item, action, target) => {
            //            if (item.Name == "Venomous Spit") {
            //                int damage = item.WeaponProperties.DamageDieCount;
            //                target.AddQEffect(QEffect.PersistentDamage($"{damage}", DamageKind.Poison));
            //                return true;
            //            }
            //            return false;
            //        }
            //    });
            //});

            //yield return new TrueFeat(ModManager.RegisterFeatName("Nagaji Spell Mysteries"), 5, "", "", new Trait[] { tNagaji }, null)
            //.WithOnCreature(creature => {
            //    creature.GetOrCreateSpellcastingSource(SpellcastingKind.Innate, tNagaji, Ability.Charisma, Trait.Occult).WithSpells(new SpellId[1] { SpellId.Heal }, 1);
            //})
            //.WithRulesBlockForSpell(SpellId.Heal, tNagaji)
            //.WithPrerequisite(sheet => sheet.HasFeat(nagajiSpellFamiliarity), "at least one innate spell from a nagaji heritage or ancestry feat");

        }

        private static IEnumerable<Feat> LoadHeritages() {
            yield return new HeritageSelectionFeat(ftHoodedNagaji, "You bear the hooded head of a spitting cobra, and like such cobras, you can shoot streams of venom from your mouth.",
                "You gain a venomous spit ranged unarmed attack with a range increment of 10 feet that deals 1d4 poison damage. On a critical hit, the target takes persistent poison damage " +
                "equal to the number of weapon damage dice. Your spit doesn't have a weapon group or a critical specialization effect.\n\nYour primary Nagaji unarmed attack is your fangs, which deal piercing damage.")
            .WithOnCreature((sheet, creature) => {
                creature.AddQEffect(new QEffect() {
                    AdditionalUnarmedStrike = new Item(IllustrationName.AcidSplash, "Venomous Spit", new Trait[] { Trait.Unarmed, Trait.Ranged }).WithWeaponProperties(new WeaponProperties("1d4", DamageKind.Poison) { Sfx = SfxName.AcidSplash, VfxStyle = new VfxStyle(1, ProjectileKind.Arrow, IllustrationName.AcidSplash ) }.WithRangeIncrement(2)),
                    AfterYouDealDamage = async (user, action, target) => {
                        if (action.Item != null && action.Item.Name == "Venomous Spit" && action.CheckResult == CheckResult.CriticalSuccess) {
                            int damage = action.TrueDamageFormula.ToString()[0] - '0';
                            target.AddQEffect(QEffect.PersistentDamage($"{damage}", DamageKind.Poison));
                        }
                    }
                });
                creature.AddQEffect(new QEffect() {
                    AdditionalUnarmedStrike = new Item(IllustrationName.Fang, "Fangs", new Trait[] { Trait.Unarmed, Trait.Finesse, Trait.Brawling }).WithWeaponProperties(new WeaponProperties("1d6", DamageKind.Piercing)),
                });
            });

            yield return new HeritageSelectionFeat(ModManager.RegisterFeatName("Sacred Nagaji"), "You stand out from most nagaji, with the upper body of a beautiful human and the lower body of a green or white snake.",
                "You gain a +2 circumstance bonus on your Fortitude or Reflex DC against attempts to Grapple or Trip you. This bonus also applies to saving throws against effects that would grab you, restrain you." +
                "\n\nYour primary Nagaji unarmed attack is your tail, which deals bludgeoning damage.")
            .WithOnCreature((sheet, creature) => {
                creature.AddQEffect(new QEffect() {
                    BonusToDefenses = (self, action, defence) => {
                        if (action == null)
                            return null;

                        if (action.ActionId == ActionId.Trip || action.ActionId == ActionId.Grapple || action.Traits.FirstOrDefault(t => new Trait[] { Trait.Trip, Trait.Grab }.Contains(t)) != Trait.None) {
                            return new Bonus(2, BonusType.Circumstance, "Sacred Nagaji");
                        }
                        return null;
                    },
                    AdditionalUnarmedStrike = new Item(illWhip, "Tail", new Trait[] { Trait.Unarmed, Trait.Finesse, Trait.Brawling }).WithWeaponProperties(new WeaponProperties("1d6", DamageKind.Bludgeoning)),
                });
            });

            yield return new HeritageSelectionFeat(ModManager.RegisterFeatName("Venomshield Nagaji"), "Your intrinsic connection to nagas and mundane serpents grants you an innate resistance to toxins of every sort.",
                "You gain resistance to poison equal to half your level (minimum 1 resistance), and you gain a +1 circumstance bonus to all saving throws against poison.\n\nYour primary Nagaji unarmed attack is your fangs, which deal piercing damage.")
            .WithOnCreature((sheet, creature) => {
                creature.AddQEffect(new QEffect() {
                    BonusToDefenses = (self, action, defence) => {
                        if (action == null)
                            return null;

                        if (action.HasTrait(Trait.Poison)) {
                            return new Bonus(1, BonusType.Circumstance, "Venomshield Nagaji");
                        }
                        return null;
                    },
                    StateCheck = self => {
                        self.Owner.WeaknessAndResistance.AddResistance(DamageKind.Poison, Math.Max(1, self.Owner.Level / 2));
                    },
                    AdditionalUnarmedStrike = new Item(IllustrationName.Fang, "Fangs", new Trait[] { Trait.Unarmed, Trait.Finesse, Trait.Brawling }).WithWeaponProperties(new WeaponProperties("1d6", DamageKind.Piercing)),
                });
            });
        }


    }
}