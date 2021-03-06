﻿namespace Techies
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Extensions;

    using SharpDX;
    using SharpDX.Direct3D9;

    internal class Techies
    {
        #region Static Fields

        private static readonly Dictionary<Unit, float> RemoteMinesDb = new Dictionary<Unit, float>();

        private static bool aghanims;

        private static uint Case = 1;

        private static Dictionary<float, bool> enabledHeroes = new Dictionary<float, bool>();

        private static Ability forceStaff;

        private static Ability landMines;

        private static float landMinesDmg;

        private static uint landMinesLevel;

        private static bool loaded;

        private static Hero me;

        private static Font panelText;

        private static Ability remoteMines;

        private static float remoteMinesDmg;

        private static uint remoteMinesLevel;

        private static float remoteMinesRadius;

        private static Ability suicideAttack;

        private static float suicideAttackDmg;

        private static uint suicideAttackLevel;

        private static float suicideAttackRadius;

        private static Font text;

        #endregion

        #region Public Methods and Operators

        public static void Init()
        {
            Game.OnUpdate += Game_OnUpdate;
            ObjectMgr.OnAddEntity += ObjectMgr_OnAddEntity;
            ObjectMgr.OnRemoveEntity += ObjectMgr_OnRemoveEntity;
            loaded = false;
            text = new Font(
                Drawing.Direct3DDevice9,
                new FontDescription
                    {
                        FaceName = "Tahoma", Height = 13, OutputPrecision = FontPrecision.Default,
                        Quality = FontQuality.Default
                    });

            panelText = new Font(
                Drawing.Direct3DDevice9,
                new FontDescription
                    {
                        FaceName = "Tahoma", Height = 25, OutputPrecision = FontPrecision.Raster,
                        Quality = FontQuality.ClearTypeNatural, CharacterSet = FontCharacterSet.Default, Italic = false,
                        MipLevels = 0, PitchAndFamily = FontPitchAndFamily.Swiss, Weight = FontWeight.ExtraBold, Width = 6
                    });

            Drawing.OnPreReset += Drawing_OnPreReset;
            Drawing.OnPostReset += Drawing_OnPostReset;
            Drawing.OnEndScene += Drawing_OnEndScene;
            AppDomain.CurrentDomain.DomainUnload += CurrentDomainDomainUnload;
            Game.OnWndProc += Game_OnWndProc;
        }

        #endregion

        #region Methods

        private static void CurrentDomainDomainUnload(object sender, EventArgs e)
        {
            text.Dispose();
            panelText.Dispose();
        }

        private static void Drawing_OnEndScene(EventArgs args)
        {
            if (Drawing.Direct3DDevice9 == null || Drawing.Direct3DDevice9.IsDisposed || !Game.IsInGame)
            {
                return;
            }

            var player = ObjectMgr.LocalPlayer;
            if (player == null || player.Team == Team.Observer)
            {
                return;
            }
            var sign = "#Techies: Detonate on Creeps Disabled! AutoSuicide Disabled | [L] for toggle";
            switch (Case)
            {
                case 2:
                    sign = "#Techies: Detonate on Creeps Enabled! AutoSuicide Enabled | [L] for toggle";
                    break;
                case 3:
                    sign = "#Techies: Detonate on Creeps Disabled! AutoSuicide Enabled | [L] for toggle";
                    break;
                case 4:
                    sign = "#Techies: Detonate on Creeps Enabled! AutoSuicide Disabled | [L] for toggle";
                    break;
            }
            text.DrawText(null, sign, 5, 128, Color.DarkOrange);
            if (!Utils.SleepCheck("drawPanel") || me == null)
            {
                return;
            }
            try
            {
                foreach (var hero in
                    ObjectMgr.GetEntities<Player>()
                        .Where(x => x != null && x.Hero != null && x.Hero.Team == me.GetEnemyTeam())
                        .Select(play => play.Hero))
                {
                    bool enabled;
                    if (!enabledHeroes.TryGetValue(hero.Handle, out enabled))
                    {
                        enabledHeroes[hero.Handle] = true;
                    }
                    var health = hero.Health;
                    if (!hero.IsAlive)
                    {
                        health = hero.MaximumHealth;
                    }
                    var sizeX = HUDInfo.GetTopPanelSizeX(hero);
                    var x = HUDInfo.GetTopPanelPosition(hero).X;
                    var sizey = HUDInfo.GetTopPanelSizeY(hero) * 1.4;
                    if (remoteMinesDmg > 0)
                    {
                        var dmg = hero.DamageTaken(remoteMinesDmg, DamageType.Magical, me, false);
                        var remoteNumber = Math.Ceiling(health / dmg);
                        panelText.DrawText(
                            null,
                            remoteNumber.ToString(CultureInfo.InvariantCulture),
                            (int)(x + sizeX / 3.6),
                            (int)sizey,
                            enabled ? Color.Green : Color.DimGray);
                    }
                    if (landMinesDmg > 0)
                    {
                        var dmg = hero.DamageTaken(landMinesDmg, DamageType.Physical, me, false);
                        var landNumber = Math.Ceiling(health / dmg);
                        panelText.DrawText(
                            null,
                            landNumber.ToString(CultureInfo.InvariantCulture),
                            (int)x,
                            (int)sizey,
                            enabled ? Color.Red : Color.DimGray);
                    }
                    if (suicideAttackDmg > 0)
                    {
                        var dmg = hero.DamageTaken(suicideAttackDmg, DamageType.Magical, me, false);
                        var canKill = dmg > health;
                        panelText.DrawText(
                            null,
                            canKill ? "Yes" : "No",
                            canKill ? (int)(x + sizeX / 2) : (int)(x + sizeX / 1.7),
                            (int)sizey,
                            enabled ? Color.DarkOrange : Color.DimGray);
                    }
                }
            }
            catch (Exception)
            {
                //do nothin
            }
        }

        private static void Drawing_OnPostReset(EventArgs args)
        {
            text.OnResetDevice();
            panelText.OnResetDevice();
        }

        private static void Drawing_OnPreReset(EventArgs args)
        {
            text.OnLostDevice();
            panelText.OnLostDevice();
        }

        private static Dictionary<int, Ability> FindDetonatableBombs(
            Unit hero,
            Vector3 pos,
            IEnumerable<KeyValuePair<Unit, float>> bombs)
        {
            var possibleBombs = bombs.Where(x => x.Key.Distance2D(pos) <= remoteMinesRadius);
            var detonatableBombs = new Dictionary<int, Ability>();
            var dmg = 0f;
            foreach (var bomb in possibleBombs)
            {
                if (dmg > 0)
                {
                    var takenDmg = hero.DamageTaken(dmg, DamageType.Magical, me, false);
                    if (takenDmg >= hero.Health)
                    {
                        break;
                    }
                }
                detonatableBombs[detonatableBombs.Count + 1] = bomb.Key.Spellbook.Spell1;
                dmg += bomb.Value;
            }
            dmg = hero.DamageTaken(dmg, DamageType.Magical, me, false);
            return dmg < hero.Health ? null : detonatableBombs;
        }

        private static void Game_OnUpdate(EventArgs args)
        {
            if (!loaded)
            {
                me = ObjectMgr.LocalHero;
                if (!Game.IsInGame || me == null || me.ClassID != ClassID.CDOTA_Unit_Hero_Techies)
                {
                    return;
                }
                loaded = true;
                remoteMines = me.Spellbook.SpellR;
                suicideAttack = me.Spellbook.SpellE;
                landMines = me.Spellbook.SpellQ;
                enabledHeroes = new Dictionary<float, bool>();
                forceStaff = null;
                Console.WriteLine("#Techies: Loaded!");
            }

            if (!Game.IsInGame || me == null || me.ClassID != ClassID.CDOTA_Unit_Hero_Techies)
            {
                loaded = false;
                Console.WriteLine("#Techies: Unloaded!");
                return;
            }

            if (Game.IsPaused)
            {
                return;
            }

            if (forceStaff == null)
            {
                forceStaff = me.Inventory.Items.FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Item_ForceStaff);
            }

            var suicideLevel = suicideAttack.Level;

            if (suicideAttackLevel != suicideLevel)
            {
                var firstOrDefault = suicideAttack.AbilityData.FirstOrDefault(x => x.Name == "damage");
                if (firstOrDefault != null)
                {
                    suicideAttackDmg = firstOrDefault.GetValue(suicideLevel - 1);
                }
                var abilityData = suicideAttack.AbilityData.FirstOrDefault(x => x.Name == "small_radius");
                if (abilityData != null)
                {
                    suicideAttackRadius = abilityData.Value;
                }
                suicideAttackLevel = suicideLevel;
            }

            var bombLevel = remoteMines.Level;

            if (remoteMinesLevel != bombLevel)
            {
                if (me.AghanimState())
                {
                    var firstOrDefault = remoteMines.AbilityData.FirstOrDefault(x => x.Name == "damage_scepter");
                    if (firstOrDefault != null)
                    {
                        remoteMinesDmg = firstOrDefault.GetValue(bombLevel - 1);
                    }
                }
                else
                {
                    var firstOrDefault = remoteMines.AbilityData.FirstOrDefault(x => x.Name == "damage");
                    if (firstOrDefault != null)
                    {
                        remoteMinesDmg = firstOrDefault.GetValue(bombLevel - 1);
                    }
                }
                var abilityData = remoteMines.AbilityData.FirstOrDefault(x => x.Name == "radius");
                if (abilityData != null)
                {
                    remoteMinesRadius = abilityData.Value;
                }
                remoteMinesLevel = bombLevel;
            }

            var landMineslvl = landMines.Level;

            if (landMinesLevel != landMineslvl)
            {
                var firstOrDefault = landMines.AbilityData.FirstOrDefault(x => x.Name == "damage");
                if (firstOrDefault != null)
                {
                    landMinesDmg = firstOrDefault.GetValue(landMineslvl - 1);
                }
                landMinesLevel = landMineslvl;
            }

            if (!aghanims && me.AghanimState())
            {
                var firstOrDefault = remoteMines.AbilityData.FirstOrDefault(x => x.Name == "damage_scepter");
                if (firstOrDefault != null)
                {
                    remoteMinesDmg = firstOrDefault.GetValue(bombLevel - 1);
                }
                aghanims = true;
            }

            //var enemyHeroes =
            //    ObjectMgr.GetEntities<Hero>()
            //        .Where(
            //            x =>
            //            x.Team == me.GetEnemyTeam() && x.IsAlive && x.IsVisible && !x.IsMagicImmune()
            //            && x.Modifiers.All(y => y.Name != "modifier_abaddon_borrowed_time")
            //            && Utils.SleepCheck(x.ClassID.ToString()) && !x.IsIllusion);
            var bombs =
                RemoteMinesDb.Where(
                    x => x.Key.Spellbook.Spell1 != null && x.Key.Spellbook.Spell1.CanBeCasted() && x.Key.IsAlive);
            var bombsArray = bombs as KeyValuePair<Unit, float>[] ?? bombs.ToArray();
            try
            {
                foreach (var hero in
                    ObjectMgr.GetEntities<Player>()
                        .Where(
                            x =>
                            x != null && x.Hero != null && x.Hero.Team == me.GetEnemyTeam() && x.Hero.IsAlive
                            && x.Hero.IsVisible)
                        .Select(play => play.Hero))
                {
                    bool enabled;
                    if (!enabledHeroes.TryGetValue(hero.Handle, out enabled) || !enabled)
                    {
                        continue;
                    }
                    var heroDistance = me.Distance2D(hero);
                    var nearbyBombs = bombsArray.Any(x => x.Key.Distance2D(hero) <= remoteMinesRadius + 500);
                    if (nearbyBombs)
                    {
                        CheckBombDamageAndDetonate(hero, bombsArray);
                    }
                    if (heroDistance < 400 && suicideAttackLevel > 0 && me.IsAlive && (Case == 2 || Case == 3))
                    {
                        SuicideKillSteal(hero);
                    }
                    if (forceStaff == null || !(heroDistance <= forceStaff.CastRange) || !Utils.SleepCheck("forcestaff")
                        || bombsArray.Any(x => x.Key.Distance2D(hero) <= remoteMinesRadius)
                        || Prediction.IsTurning(hero) || !forceStaff.CanBeCasted())
                    {
                        continue;
                    }

                    var data =
                        Prediction.TrackTable.ToArray()
                            .FirstOrDefault(
                                unitData => unitData.UnitName == hero.Name || unitData.UnitClassID == hero.ClassID);

                    if (data != null)
                    {
                        var turnTime = me.GetTurnTime(hero);
                        var forcePosition = hero.Position;
                        if (hero.NetworkActivity == NetworkActivity.Move)
                        {
                            forcePosition = Prediction.InFront(
                                hero,
                                (float)((turnTime + Game.Ping / 1000) * hero.MovementSpeed));
                        }
                        forcePosition +=
                            VectorExtensions.FromPolarCoordinates(1f, hero.NetworkRotationRad + data.RotSpeed)
                                .ToVector3() * 600;
                        var possibleBombs =
                            bombsArray.Any(x => x.Key.Distance2D(forcePosition) <= (remoteMinesRadius - 75));
                        if (!possibleBombs)
                        {
                            continue;
                        }
                        var dmg = CheckBombDamage(hero, forcePosition, bombsArray);
                        if (!(dmg >= hero.Health))
                        {
                            continue;
                        }
                    }
                    forceStaff.UseAbility(hero);
                    Utils.Sleep(250, "forcestaff");
                }
            }
            catch (Exception)
            {
                //aa
            }
            if (!(Case == 2 || Case == 4))
            {
                return;
            }
            var creeps =
                ObjectMgr.GetEntities<Creep>()
                    .Where(
                        x =>
                        (x.ClassID == ClassID.CDOTA_BaseNPC_Creep_Lane || x.ClassID == ClassID.CDOTA_BaseNPC_Creep_Siege)
                        && x.IsAlive && x.IsVisible && x.IsSpawned && x.Team == me.GetEnemyTeam());

            var enumerable = creeps as Creep[] ?? creeps.ToArray();

            foreach (var data in (from creep in enumerable
                                  let nearbyBombs =
                                      bombsArray.Any(x => x.Key.Distance2D(creep) <= remoteMinesRadius + 500)
                                  where nearbyBombs
                                  let detonatableBombs = FindDetonatableBombs(creep, creep.Position, bombsArray)
                                  where detonatableBombs != null
                                  let nearbyCreeps =
                                      enumerable.Count(
                                          x =>
                                          x.Distance2D(creep) <= remoteMinesRadius
                                          && CheckBombDamage(x, x.Position, bombsArray) >= x.Health)
                                  where nearbyCreeps > 3
                                  select detonatableBombs).SelectMany(
                                      detonatableBombs =>
                                      detonatableBombs.Where(data => Utils.SleepCheck(data.Value.Handle.ToString()))))
            {
                data.Value.UseAbility();
                Utils.Sleep(250, data.Value.Handle.ToString());
            }
        }

        private static void Game_OnWndProc(WndEventArgs args)
        {
            if (!Game.IsChatOpen && args.Msg == (ulong)Utils.WindowsMessages.WM_KEYUP && args.WParam == 'L')
            {
                if (Case == 4)
                {
                    Case = 1;
                }
                else
                {
                    Case += 1;
                }
            }
            if (args.Msg != (ulong)Utils.WindowsMessages.WM_LBUTTONDOWN)
            {
                return;
            }
            try
            {
                foreach (var hero in
                    from play in
                        ObjectMgr.GetEntities<Player>()
                        .Where(x => x != null && x.Hero != null && x.Hero.Team == me.GetEnemyTeam())
                    select play.Hero
                    into hero
                    let sizeX = (float)HUDInfo.GetTopPanelSizeX(hero)
                    let x = HUDInfo.GetTopPanelPosition(hero).X
                    let sizey = HUDInfo.GetTopPanelSizeY(hero) * 1.4
                    where Utils.IsUnderRectangle(Game.MouseScreenPosition, x, 0, sizeX, (float)(sizey * 1.4))
                    select hero)
                {
                    bool enabled;
                    if (enabledHeroes.TryGetValue(hero.Handle, out enabled))
                    {
                        enabledHeroes[hero.Handle] = !enabled;
                    }
                }
            }
            catch (Exception)
            {
                //
            }
        }

        private static float CheckBombDamage(Unit hero, Vector3 pos, IEnumerable<KeyValuePair<Unit, float>> bombs)
        {
            var dmg =
                bombs.Where(x => x.Key.Distance2D(pos) <= remoteMinesRadius).Sum(possibleBomb => possibleBomb.Value);
            return hero.DamageTaken(dmg, DamageType.Magical, me, false);
        }

        private static void CheckBombDamageAndDetonate(Unit hero, KeyValuePair<Unit, float>[] bombs)
        {
            var pos = hero.Position;
            if (hero.NetworkActivity == NetworkActivity.Move)
            {
                pos = Prediction.InFront(hero, (Game.Ping / 1000) * hero.MovementSpeed);
            }
            CheckBombDamageAndDetonate(hero, pos, bombs);
        }

        private static void CheckBombDamageAndDetonate(
            Unit hero,
            Vector3 pos,
            IEnumerable<KeyValuePair<Unit, float>> bombs)
        {
            if (!Utils.SleepCheck(hero.ClassID.ToString()))
            {
                return;
            }
            if (hero.Modifiers.Any(y => y.Name == "modifier_abaddon_borrowed_time"))
            {
                return;
            }
            var pos1 = pos;
            var possibleBombs =
                bombs.Where(
                    x =>
                    x.Key.Distance2D(pos1) <= remoteMinesRadius && x.Key.Distance2D(hero.Position) <= remoteMinesRadius
                    && (remoteMinesRadius - x.Key.Distance2D(pos1) - 50) / hero.MovementSpeed < (Game.Ping / 1000));

            var detonatableBombs = new Dictionary<int, Ability>();
            var dmg = 0f;
            foreach (var bomb in possibleBombs)
            {
                if (dmg > 0)
                {
                    var takenDmg = hero.DamageTaken(dmg, DamageType.Magical, me, false);
                    if (takenDmg >= hero.Health)
                    {
                        break;
                    }
                }
                detonatableBombs[detonatableBombs.Count + 1] = bomb.Key.Spellbook.Spell1;
                dmg += bomb.Value;
            }
            dmg = hero.DamageTaken(dmg, DamageType.Magical, me, false);
            if (dmg < hero.Health)
            {
                return;
            }
            if (hero.NetworkActivity == NetworkActivity.Move)
            {
                pos = Prediction.InFront(hero, ((Game.Ping / 1000) * hero.MovementSpeed));
                var stop = false;
                foreach (var mine in
                    detonatableBombs.Where(data => Utils.SleepCheck(data.Value.Handle.ToString()))
                        .Select(data => data.Value.Owner)
                        .Where(
                            mine =>
                            mine.Distance2D(pos) > remoteMinesRadius
                            || (remoteMinesRadius - mine.Distance2D(pos) - 50) / hero.MovementSpeed
                            > (Game.Ping / 1000 + detonatableBombs.Count * 0.002)))
                {
                    stop = true;
                }
                if (stop)
                {
                    return;
                }
            }
            foreach (var data in detonatableBombs.Where(data => Utils.SleepCheck(data.Value.Handle.ToString())))
            {
                data.Value.UseAbility();
                Utils.Sleep(250, data.Value.Handle.ToString());
            }
            Utils.Sleep(1000, hero.ClassID.ToString());
        }

        private static void ObjectMgr_OnAddEntity(EntityEventArgs args)
        {
            var ent = args.Entity as Unit;
            if (ent != null && (ent.ClassID == ClassID.CDOTA_NPC_TechiesMines))
            {
                RemoteMinesDb.Add(ent, remoteMinesDmg);
            }
        }

        private static void ObjectMgr_OnRemoveEntity(EntityEventArgs args)
        {
            var ent = args.Entity as Unit;
            if (ent != null && (ent.ClassID == ClassID.CDOTA_NPC_TechiesMines))
            {
                RemoteMinesDb.Remove(ent);
            }
        }

        private static void SuicideKillSteal(Unit hero)
        {
            if (!Utils.SleepCheck("suicide"))
            {
                return;
            }
            var dmg = hero.DamageTaken(suicideAttackDmg, DamageType.Physical, me, true);
            //Console.WriteLine(dmg);
            if (!(dmg >= hero.Health))
            {
                return;
            }
            var pos = hero.NetworkPosition;
            if (hero.NetworkActivity == (NetworkActivity)1502)
            {
                pos = Prediction.InFront(hero, (float)((Game.Ping / 1000 + me.GetTurnTime(hero)) * hero.MovementSpeed));
            }
            if (pos.Distance2D(me) < hero.Distance2D(me))
            {
                pos = hero.Position;
            }
            if (!(pos.Distance2D(me) < suicideAttackRadius))
            {
                return;
            }
            if (me.Distance2D(pos) > 100)
            {
                pos = (pos - me.Position) * 99 / pos.Distance2D(me) + me.Position;
            }
            suicideAttack.UseAbility(pos);
            Utils.Sleep(500, "suicide");
        }

        #endregion
    }
}