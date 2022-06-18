using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ModLoader;

using static Terraria.ModLoader.ModContent;
using static SummonersShineExampleMod.SummonersShineCompat;
using static SummonersShineExampleMod.SummonersShineCompat.MinionPowerCollection;
using Terraria.ID;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;

namespace SummonersShineExampleMod.Minions.ExampleActiveSpecialAbilityMinion
{
    public class ExampleActiveSpecialAbilityMinionItem : ModItem
    {
        public override void SetStaticDefaults()
        {
            ItemID.Sets.StaffMinionSlotsRequired[Item.type] = 1;
            if (SummonersShine != null)
            {
                MinionPowerCollection minionPower = new MinionPowerCollection();
                minionPower.AddMinionPower(20, MinionPowerScalingType.multiply);
                ModSupport_AddItemStatics(Item.type, SpecialAbilityFindTarget, SpecialAbilityFindMinions, minionPower, 300, true);
            }
        }

        List<Projectile> SpecialAbilityFindMinions(Player player, Item item, List<Projectile> valid)
        {
            return valid;
        }
        Entity SpecialAbilityFindTarget(Player player, Vector2 mouseWorld)
        {
            NPC npc = new(); //dummy
            npc.position = mouseWorld;
            return npc; //this basically returns a point
        }

        public override bool AltFunctionUse(Player player)
        {
            return true;
        }
        public override void SetDefaults()
        {
            // So the weapon doesn't damage like a sword while swinging 
            Item.noMelee = true;
            Item.useStyle = ItemUseStyleID.HoldUp;
            // The damage type of this weapon
            Item.DamageType = DamageClass.Summon;
            Item.damage = 30;
            Item.knockBack = 0.01f;
            Item.crit = 30;
            Item.useTime = 32;
            Item.useAnimation = 32;
            Item.buffType = BuffType<ExampleActiveSpecialAbilityMinionBuff>();
            Item.shoot = ProjectileType<ExampleActiveSpecialAbilityMinion>();
        }
        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            if (player.altFunctionUse != 2)
            {
                player.AddBuff(Item.buffType, 2, true);
                position = Main.MouseWorld;

                player.SpawnMinionOnCursor(Item.GetSource_FromThis(), player.whoAmI, type, Item.damage, knockback);
            }
        }
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            return false;
        }
    }
    public class ExampleActiveSpecialAbilityMinionBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            if (player.ownedProjectileCounts[ProjectileType<ExampleActiveSpecialAbilityMinion>()] > 0)
            {
                player.buffTime[buffIndex] = 2;
            }
            else
            {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }
    public class ExampleActiveSpecialAbilityMinion : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            // Denotes that this projectile is a pet or minion
            Main.projPet[Projectile.type] = true;
            // This is needed so your minion can properly spawn when summoned and replaced when other minions are summoned
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Projectile.type] = true;
            if (SummonersShine != null)
            {
                ProjectileOnCreate_SetMinionTrackingAcceleration(Projectile.type, 100f);
                ProjectileOnCreate_SetMinionTrackingImperfection(Projectile.type, 5f);
                ProjectileOnCreate_SetMaxEnergy(Projectile.type, 300);

                ProjectileOnCreate_SetMinionOnSpecialAbilityUsed(Projectile.type, OnSpecialAbilityUsed);
            }

            Main.projFrames[Projectile.type] = 4;
        }

        public override void SetDefaults()
        {
            // Only determines the damage type
            Projectile.minion = true;
            // Amount of slots this minion occupies from the total minion slots available to the player (more on that later)
            Projectile.minionSlots = 1;
            // Needed so the minion doesn't despawn on collision with enemies or tiles
            Projectile.penetrate = -1;
            // 0 cooldown
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = -1;
            Projectile.scale = 1;
            Projectile.width = 32;
            Projectile.height = 48;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
        }
        float attackCooldown { get { return Projectile.ai[1]; } set { Projectile.ai[1] = value; } }

        public override bool? CanCutTiles()
        {
            return false;
        }

        public override bool MinionContactDamage()
        {
            return true;
        }
        private void OnSpecialAbilityUsed(Projectile minion, Entity target, int castingSpecialAbilityType, bool fromServer)
        {
            ModSupport_SetVariable_ProjData(minion, ProjectileDataVariableType.energy, 0f);
            Vector2 disp = target.Center - minion.Center;
            float dist = disp.Length();
            float maxDist = minion.SummonersShine_GetMinionPower(0) * 16;
            float extras = maxDist / 6;
            if (dist > maxDist)
            {
                disp *= maxDist / dist;
                dist = maxDist;
            }
            else
                extras += (maxDist - dist) / 2;

            int iters = (int)(dist + extras + extras) / 16 + 1;

            Vector2 start = minion.Center;
            Vector2 end = minion.Center + disp;
            disp = disp.SafeNormalize(Vector2.One);

            for (int j = 0; j < 32; j ++)
            {
                float dispPos = Main.rand.NextFloat(-1, 1);
                Dust dust = Dust.NewDustDirect(start + new Vector2(disp.Y, -disp.X) * dispPos * 16 + disp * (1 - dispPos) * 8, 0, 0, DustID.DesertTorch, 0, 0, 0, Color.Yellow, 1);
                dust = Dust.NewDustDirect(end + new Vector2(disp.Y, -disp.X) * dispPos * 16 + disp * (1 - dispPos) * 8, 0, 0, DustID.CoralTorch, 0, 0, 0, Color.Yellow, 1);
                dust = Dust.NewDustDirect(end + new Vector2(disp.Y, -disp.X) * dispPos * 16 + disp * (1 - dispPos) * 8, 0, 0, DustID.WaterCandle, 0, 0, 0, Color.Yellow, 1);
            }


            minion.friendly = true;

            Vector2 pos = start - disp * extras;
            for (int i = 0; i < iters; i++)
            {
                for (int j = 0; j < 16; j += 4)
                {
                    Dust dust = Dust.NewDustDirect(pos + disp * j + new Vector2(disp.Y, -disp.X) * Main.rand.NextFloat(-4, 4), 0, 0, DustID.UltraBrightTorch, 0, 0, 0, Color.LightBlue);
                    dust.noGravity = true;
                    dust.velocity = disp;
                }

                minion.Center = pos;
                minion.Damage();
                pos += disp * 16;
            }
            minion.Center = end;
            minion.Damage();

            minion.friendly = false;
        }
        public override void AI()
        {

            //To ensure the minion despawns with the buff
            Player player = Main.player[Projectile.owner];
            if (player.dead || !player.active)
            {
                player.ClearBuff(BuffType<ExampleActiveSpecialAbilityMinionBuff>());
            }
            if (player.HasBuff(BuffType<ExampleActiveSpecialAbilityMinionBuff>()))
            {
                Projectile.timeLeft = 2;
            }

            int action = 0;
            int startAttackRange = 1400;
            int targetIndex = -1;

            Projectile.Minion_FindTargetInRange(startAttackRange, ref targetIndex, false);

            if (targetIndex != -1)
            {
                if (attackCooldown == 0)
                {
                    NPC target = Main.npc[targetIndex];
                    Vector2 diff = target.Center - Projectile.Center;
                    diff = diff.SafeNormalize(Vector2.Zero);

                    for (int j = 0; j < 16; j++)
                    {
                        Dust dust = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(16, 16), 0, 0, DustID.DesertTorch, 0, 0, 0, Color.Yellow);
                        dust.noGravity = true;
                        dust = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(16, 16), 0, 0, DustID.CoralTorch, 0, 0, 0, Color.Yellow);
                        dust = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(16, 16), 0, 0, DustID.WaterCandle, 0, 0, 0, Color.Yellow);
                    }
                    Projectile.Center = target.Center;

                    for (int j = 0; j < 16; j++)
                    {
                        Dust dust = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(16, 16), 0, 0, DustID.DesertTorch, 0, 0, 0, Color.Yellow);
                        dust.noGravity = true;
                        dust = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(16, 16), 0, 0, DustID.CoralTorch, 0, 0, 0, Color.Yellow);
                        dust = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(16, 16), 0, 0, DustID.WaterCandle, 0, 0, 0, Color.Yellow);
                    }
                    Projectile.friendly = true;
                    Projectile.Damage();
                    Projectile.friendly = false;
                    Projectile.position -= diff * 16;
                    Projectile.velocity = -diff * 4f;
                    Projectile.spriteDirection = Math.Sign(diff.X);

                    Projectile.frameCounter = 12 * 2;
                    attackCooldown = 30;
                }
                action = 1;
            }
            if (attackCooldown > 0)
                attackCooldown--;

            Projectile.frameCounter += 1;

            Vector2 AFKPos = player.Center + new Vector2(-player.direction * 48, 0) * (Projectile.minionPos + 1);

            switch (action)
            {
                case 0:
                    {
                        Vector2 diff = AFKPos - Projectile.Center;
                        if (diff.LengthSquared() > 2000 * 2000)
                        {
                            Projectile.position = AFKPos;
                        }
                        if (diff.LengthSquared() > Math.Max(4, Projectile.velocity.LengthSquared()))
                        {
                            diff = diff.SafeNormalize(Vector2.Zero);
                            Projectile.velocity += diff * 0.5f;
                            Projectile.velocity *= 0.9f;
                            if (Projectile.velocity.X < -1f)
                            {
                                Projectile.spriteDirection = -1;
                            }
                            else if (Projectile.velocity.X > 1f)
                            {
                                Projectile.spriteDirection = 1;
                            }
                            else
                                Projectile.spriteDirection = player.direction;
                        }
                        else {
                            Projectile.velocity = diff;
                            Projectile.spriteDirection = player.direction;
                        }
                        if (Projectile.frameCounter >= 24)
                            Projectile.frameCounter = 0;
                        if (SummonersShine != null)
                        {
                            ModSupport_SetVariable_ProjData(Projectile, ProjectileDataVariableType.trackingState, MinionTracking_State.Normal);
                            ModSupport_SetVariable_ProjData(Projectile, ProjectileDataVariableType.minionTrackingImperfection, 5f);
                        }
                    }
                    break;
                case 1:
                    {

                        if (Projectile.frameCounter >= 72)
                            Projectile.frameCounter = 0;
                        if (SummonersShine != null)
                        {
                            ModSupport_SetVariable_ProjData(Projectile, ProjectileDataVariableType.trackingState, MinionTracking_State.Retreating);
                            ModSupport_SetVariable_ProjData(Projectile, ProjectileDataVariableType.minionTrackingImperfection, 5f);
                        }
                    }
                    break;
            }

            //make the minions not be so close to each other
            for (int x = 0; x < Main.maxProjectiles; x++)
            {
                if (x == Projectile.whoAmI)
                    continue;
                Projectile test = Main.projectile[x];
                if (test != null && test.active && test.owner == Projectile.owner && test.type == Projectile.type)
                {
                    Vector2 disp = Projectile.Center - test.Center;
                    if (disp == Vector2.Zero)
                    {
                        disp = Main.rand.NextVector2Circular(1, 1);
                    }
                    float magnitude = disp.Length();
                    if (magnitude < 16 * 5)
                    {
                        Projectile.velocity += disp.SafeNormalize(Vector2.Zero) * 0.1f;
                    }
                }
            }
            int step = Projectile.frameCounter / 12;
            switch (step)
            {
                case 0:
                    Projectile.frame = 0;
                    break;
                case 1:
                    Projectile.frame = 1;
                    break;
                case 2:
                    Projectile.frame = 2;
                    break;
                case 3:
                    Projectile.frame = 2;
                    break;
                case 4:
                    Projectile.frame = 3;
                    break;
                case 5:
                    Projectile.frame = 3;
                    break;
            }
        }
    }
}
