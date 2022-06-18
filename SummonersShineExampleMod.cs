using Microsoft.Xna.Framework.Graphics;
using System;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;

using static Terraria.ModLoader.ModContent;
using SummonersShineExampleMod.Minions.ExampleActiveSpecialAbilityMinion;

namespace SummonersShineExampleMod
{
	public class SummonersShineExampleMod : Mod
    {
        public static Texture2D ThoughtBubble;
        public override void Load()
        {
            ThoughtBubble = ModContent.Request<Texture2D>("SummonersShineExampleMod/ExampleModBubbleData", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
        }
        public override void Unload()
        {
            ThoughtBubble = null;
        }
        public override void PostSetupContent()
        {
            if (SummonersShineCompat.SummonersShine != null)
            {
                //Add this if you have blacklisted every projectile you don't want to track
                SummonersShineCompat.WhitelistMod(this, SummonersShineCompat.WhitelistModType.tracking);
                //Add this if you don't want your minion to be affected by Summoner's Shine outgoing damage changes
                SummonersShineCompat.WhitelistMod(this, SummonersShineCompat.WhitelistModType.damage);
                //This is required to display the pretty bubbles
                SummonersShineCompat.ModSupport_AddSpecialPowerDisplayData(GetSpecialPowerDisplayData);
            }
        }

        public Tuple<Texture2D, Rectangle> GetSpecialPowerDisplayData(int ItemType, int Frame)
        {
            //return empty if bubble is opening/closing
            if (Frame == 0 || Frame == 3)
                return null;
            if (ItemType == ItemType<ExampleActiveSpecialAbilityMinionItem>())
            {
                //compress frames 1/2/4/5 to 0/1/2/3
                if (Frame > 3)
                    Frame--;
                Frame--;

                //returns image. base images off bubble.png scaled 2x.
                return new(ThoughtBubble, new(Frame * 40, 0, 40, 40));
            }
            return null;
        }
    }
}