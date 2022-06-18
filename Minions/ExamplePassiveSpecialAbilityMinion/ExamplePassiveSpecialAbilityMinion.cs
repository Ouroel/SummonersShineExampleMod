using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.ModLoader.ModContent;
using static SummonersShineExampleMod.SummonersShineCompat;
using static SummonersShineExampleMod.SummonersShineCompat.MinionPowerCollection;

namespace SummonersShineExampleMod.Minions.ExamplePassiveSpecialAbilityMinion
{
    public class ExamplePassiveSpecialAbilityMinionItem : ModItem
    {
        public override void SetStaticDefaults()
        {
            ItemID.Sets.StaffMinionSlotsRequired[Item.type] = 1;

            //Always check
            if (SummonersShine != null)
            {
                // Set minion power
                MinionPowerCollection minionPower = new MinionPowerCollection();
                minionPower.AddMinionPower(100, MinionPowerScalingType.add);
                ModSupport_AddItemStatics(Item.type, null, null, minionPower, 0, true);

                //Make display pins properly reflect position
                SetBuffDisplayPinPositionsOverride(BuffType<ExamplePassiveSpecialAbilityMinionBuff>(), GetPinPositions);
            }
        }
        private Tuple<bool, float, float> GetPinPositions(int BuffType, int ItemType, List<Projectile> Projectiles)
        {
            //lower pin
            float least = -1;
            //higher pin
            float greatest = -1;
            Projectiles.ForEach(i =>
            {
                int castingSpecialAbilityTime = (int)ModSupport_GetVariable_ProjData(i, ProjectileDataVariableType.castingSpecialAbilityTime);
                float ratio = (castingSpecialAbilityTime + 1) / (float)(ExamplePassiveSpecialAbilityMinion.ManicBloodDuration * 2 + 1);
                if (least == -1 || least > ratio)
                    least = ratio;
                if (greatest == -1 || greatest < ratio)
                    greatest = ratio;
            });
            return new(true, greatest, least);
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
            Item.buffType = BuffType<ExamplePassiveSpecialAbilityMinionBuff>();
            Item.shoot = ProjectileType<ExamplePassiveSpecialAbilityMinion>();
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

    public class ExamplePassiveSpecialAbilityMinionBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            if (player.ownedProjectileCounts[ProjectileType<ExamplePassiveSpecialAbilityMinion>()] > 0)
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
    public class ExamplePassiveSpecialAbilityMinion : ModProjectile
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
                ProjectileOnCreate_SetMinionTrackingImperfection(Projectile.type, 10f);
                ProjectileOnCreate_SetMaxEnergy(Projectile.type, 0);
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
            Projectile.scale = 1;
            Projectile.width = 32;
            Projectile.height = 48;
            Projectile.netImportant = true;
        }

        float ammoCount { get { return Projectile.ai[0]; } set { Projectile.ai[0] = value; } }
        float attackCooldown { get { return Projectile.ai[1]; } set { Projectile.ai[1] = value; } }

        const float Negev_Max_Ammo = 30;

        public const int ManicBloodDuration = 600;

        bool HasAmmo()
        {
            return (ammoCount > -1 && ammoCount <= Negev_Max_Ammo);
        }
        bool DontHaveFullAmmo()
        {
            return (ammoCount > 0 && ammoCount <= Negev_Max_Ammo);
        }
        public override void AI()
        {
            //To ensure the minion despawns with the buff
            Player player = Main.player[Projectile.owner];
            if (player.dead || !player.active)
            {
                player.ClearBuff(BuffType<ExamplePassiveSpecialAbilityMinionBuff>());
            }
            if (player.HasBuff(BuffType<ExamplePassiveSpecialAbilityMinionBuff>()))
            {
                Projectile.timeLeft = 2;
            }

            int startAttackRange = 1400;
            int attackTarget = -1;
            //This function automatically sets the target of the minion. If you want to, you can set it yourself, but here we will not set it
            Projectile.Minion_FindTargetInRange(startAttackRange, ref attackTarget, true);
            
            //handle special ability stuff
            int manic = 0;
            float manicBloodModifier = 1;
            //check if minion power is enabled
            if (SummonersShine != null && IsItemMinionPowerEnabled(ItemType<ExamplePassiveSpecialAbilityMinionItem>()))
            {
                //castingSpecialAbilityTime is synced between clients
                int castingSpecialAbilityTime = (int)ModSupport_GetVariable_ProjData(Projectile, ProjectileDataVariableType.castingSpecialAbilityTime);

                if (castingSpecialAbilityTime > -1)
                {
                    int mult = 1;
                    if (castingSpecialAbilityTime > ManicBloodDuration)
                        mult = 2;
                    if (HasAmmo())
                    {
                        castingSpecialAbilityTime--;
                        ModSupport_SetVariable_ProjData(Projectile, ProjectileDataVariableType.castingSpecialAbilityTime, castingSpecialAbilityTime);
                    }
                    manicBloodModifier += (Projectile.SummonersShine_GetMinionPower(0) / 100f) * mult;
                    manic = mult;
                }

            }

            //handle attacking
            int movementType = 0;
            NPC target = null;
            bool realReload = true;
            bool attacking = false;
            if (attackTarget != -1)
            {
                target = Main.npc[attackTarget];
                movementType = 1;
                if (HasAmmo() && Projectile.tileCollide)
                {
                    float dist = target.Center.DistanceSQ(Projectile.Center);
                    if (dist <= 16 * 30 * 16 * 30)
                    {
                        if (attackCooldown <= 0)
                        {
                            Negev_Shoot(target, manic);
                            attackCooldown = 15f / manicBloodModifier;
                        }
                        Projectile.direction = (target.Center.X - Projectile.Center.X) < 0 ? -1 : 1;
                        attacking = true;
                        movementType = 2;
                    }
                }
            }
            else if (DontHaveFullAmmo() && attackCooldown <= -300)
            {
                ammoCount = -1;
                realReload = false;
            }
            Negev_Reload(manicBloodModifier, realReload);
            if (attackCooldown > -300)
                attackCooldown--;

            //Get AFK position

            Vector2 AFKPos = player.Center + new Vector2(-player.direction * 48, 0) * (Projectile.minionPos + 1);

            //handle movement
            bool tryLeaveTileCollide = false;
            switch (movementType)
            {

                case 2:
                    {
                        if (SummonersShine != null)
                        {
                            ModSupport_SetVariable_ProjData(Projectile, ProjectileDataVariableType.trackingState, MinionTracking_State.NoTracking);
                            ModSupport_SetVariable_ProjData(Projectile, ProjectileDataVariableType.minionTrackingImperfection, 10f);
                        }
                    }
                    tryLeaveTileCollide = true;
                    Projectile.velocity = Vector2.Zero;
                    break;
                case 1:
                    {
                        if (SummonersShine != null)
                        {
                            ModSupport_SetVariable_ProjData(Projectile, ProjectileDataVariableType.trackingState, MinionTracking_State.Normal);
                            ModSupport_SetVariable_ProjData(Projectile, ProjectileDataVariableType.minionTrackingImperfection, 10f);
                        }
                        Vector2 diff = target.Center - Projectile.Center;
                        diff = diff.SafeNormalize(Vector2.UnitX);
                        diff *= 16 * 5;
                        diff = target.Center - diff - Projectile.Center;
                        diff = diff.SafeNormalize(Vector2.Zero);
                        diff *= 0.5f * (manic + 1);
                        Projectile.velocity = (Projectile.velocity + diff) * 40 / 41;
                        tryLeaveTileCollide = true;
                    }
                    break;
                default:
                    {
                        if (SummonersShine != null)
                        {
                            ModSupport_SetVariable_ProjData(Projectile, ProjectileDataVariableType.trackingState, MinionTracking_State.Retreating);
                            //set it lower so the projectile is better at tailing player
                            ModSupport_SetVariable_ProjData(Projectile, ProjectileDataVariableType.minionTrackingImperfection, 2f);
                        }
                        Vector2 diff = AFKPos - Projectile.Center;
                        if (diff.LengthSquared() > 2000 * 2000)
                        {
                            Projectile.position = AFKPos;
                        }
                        if (diff.LengthSquared() > 16 * 16 * 5 * 5)
                        {
                            diff.Normalize();
                            diff *= 0.1f;
                            Projectile.velocity = (Projectile.velocity + diff) * 40 / 41;
                            Projectile.tileCollide = false;
                        }
                        else
                            tryLeaveTileCollide = true;
                    }
                    break;
            }
            if (tryLeaveTileCollide && !Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                Projectile.tileCollide = true;
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

            //animate the minion
            Projectile.frameCounter += 1;
            int step = Projectile.frameCounter / 12;
            if (step >= 6)
            {
                Projectile.frameCounter = 36;
                step = 3;
            }
            if (step < 3)
            {
                Projectile.frame = step;
            }
            if (step >= 3)
            {
                Projectile.frame = 4 - step;
            }
            if (step == 4)
                Projectile.frameCounter = 0;
            if (step == 5)
            {
                Projectile.frame = 3;
            }
            if (!attacking)
            {
                if (Projectile.velocity.X < -0.1f)
                {
                    Projectile.direction = -1;
                }
                else if (Projectile.velocity.X > 0.1f)
                {
                    Projectile.direction = 1;
                }
            }
            Projectile.spriteDirection = Projectile.direction;
        }

        public void Negev_Reload(float manicMod, bool realReload)
        {
            if (ammoCount == -1)
            {
                if (realReload)
                {
                    if (SummonersShine != null && IsItemMinionPowerEnabled(ItemType<ExamplePassiveSpecialAbilityMinionItem>()))
                    {
                        ActivateManicBlood();
                    }
                }
                Vector2 negevExhaust = Projectile.Center + new Vector2(-8 * Projectile.spriteDirection, -4);
                Vector2 negevExhaustDir = new Vector2(-2 * Projectile.spriteDirection, -4) * 0.2f;
                for (int x = 0; x < 10; x++)
                {
                    Dust dust = Dust.NewDustDirect(negevExhaust, 0, 0, DustID.GoldFlame, 0, 0);
                    dust.velocity = negevExhaustDir;
                    dust.velocity += Main.rand.NextVector2Circular(0.2f, 0.2f);
                }
                SoundEngine.PlaySound(in SoundID.Item149, Projectile.position);
                Projectile.frameCounter = 60;
            }
            if (ammoCount < 0)
                ammoCount -= manicMod;
            if (ammoCount < -90)
            {
                ammoCount = 0;
            }
        }

        public void ActivateManicBlood()
        {
            int castingSpecialAbilityTime = (int)ModSupport_GetVariable_ProjData(Projectile, ProjectileDataVariableType.castingSpecialAbilityTime);
            if (castingSpecialAbilityTime > 0)
                castingSpecialAbilityTime = ManicBloodDuration * 2;
            else
                castingSpecialAbilityTime = ManicBloodDuration;
            ModSupport_SetVariable_ProjData(Projectile, ProjectileDataVariableType.castingSpecialAbilityTime, castingSpecialAbilityTime);
        }

        public void Negev_Shoot(NPC target, int manic)
        {
            Projectile.frameCounter = 6;
            ammoCount++;
            if (ammoCount > Negev_Max_Ammo)
                ammoCount = -1;
            Vector2 negevBarrel = Projectile.Center + new Vector2(16 * Projectile.spriteDirection, 0);
            Vector2 vel = target.Center - negevBarrel;
            vel.Normalize();
            vel *= 4;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), negevBarrel, vel, NegevShot.ModType, Projectile.damage, Projectile.knockBack, Projectile.owner, manic);

            if (manic != 0)
            {
                float bloodSpurtDist = 3;
                float bloodSpurtMult = 0.5f;
                if (manic == 2) {
                    bloodSpurtDist = 4;
                    bloodSpurtMult = 2;
                    SoundEngine.PlaySound(in SoundID.Item89, Projectile.position);
                }
                else
                    SoundEngine.PlaySound(in SoundID.Item40, Projectile.position);
                for (int x = 0; x < bloodSpurtDist; x++)
                {
                    Dust dust = Dust.NewDustDirect(negevBarrel, 0, 0, DustID.Blood, 0, 0);
                    dust.velocity = vel * bloodSpurtMult;
                    dust.velocity += Main.rand.NextVector2Circular(0.2f, 0.2f);
                }
            }
            else
                SoundEngine.PlaySound(in SoundID.Item41, Projectile.position);
        }
    }

    public class NegevShot : ModProjectile {
        public static int ModType;
        float ManicBlood => Projectile.ai[0];
        public override void SetStaticDefaults()
        {
            // This is needed so your minion can properly spawn when summoned and replaced when other minions are summoned
            ModType = Projectile.type;
            ProjectileID.Sets.MinionShot[Projectile.type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.extraUpdates = 3;
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = true;
        }

        public override void AI()
        {
            switch (ManicBlood)
            {
                case 0:
                    if (Main.rand.Next(0, 2) == 0)
                    {
                        Dust dust = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.MarblePot, Projectile.velocity.X / 10, Projectile.velocity.Y / 10);
                        dust.velocity = Vector2.Zero;
                        dust.noGravity = true;
                    }
                    break;
                case 1:
                    if (Main.rand.Next(0, 2) == 0)
                    {
                        Dust dust = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.GoldFlame, Projectile.velocity.X / 10, Projectile.velocity.Y / 10);
                        dust.velocity = Vector2.Zero;
                        dust.noGravity = true;
                    }
                    break;
                case 2:
                    if (Main.rand.Next(0, 2) == 0)
                    {
                        Dust dust = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.GoldFlame, Projectile.velocity.X / 10, Projectile.velocity.Y / 10);
                        dust.velocity = Vector2.Zero;
                        dust.noGravity = true;
                    }
                    if (Main.rand.Next(0, 2) == 0)
                    {
                        Dust dust = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.SolarFlare, Projectile.velocity.X / 10, Projectile.velocity.Y / 10);
                        dust.velocity = Vector2.Zero;
                        dust.noGravity = true;
                    }
                    break;

            }
        }
        public override void Kill(int timeLeft)
        {
            for (int x = 0; x < 2; x++)
            {
                Dust dust = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.GoldFlame, Projectile.velocity.X / 10, Projectile.velocity.Y / 10);
                dust.velocity = -Projectile.velocity * 0.1f;
                dust.velocity += Main.rand.NextVector2Circular(0.2f, 0.2f);
            }
        }
    }
}
