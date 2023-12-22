using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;

namespace Autoplant
{
    [MinimumApiVersion(110)]
    public partial class Autoplant : BasePlugin
    {
        public override string ModuleName => "Autoplant";
        public override string ModuleAuthor => "zwolof";
        public override string ModuleDescription => "Brings Autoplant back to CS2";
        public override string ModuleVersion => "0.0.1";

        public override void Load(bool hotReload)
        {
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventRoundFreezeEnd>(OnFreezeTimeEnd);
        }

        private HookResult OnFreezeTimeEnd(EventRoundFreezeEnd @event, GameEventInfo info) 
        {
            var pBombCarrierController = this.GetBombCarrier();

            if(pBombCarrierController == null)
            {
                return HookResult.Continue;
            }

            if(!pBombCarrierController.PlayerPawn.Value!.InBombZone)
            {
                return HookResult.Continue;
            }

            this.CreatePlantedC4(pBombCarrierController);

            return HookResult.Continue;
        }

        public bool CreatePlantedC4(CCSPlayerController bombCarrier)
        {
            var gameRules = this.GetGameRules();

            if(bombCarrier == null || gameRules == null)
            {
                return false;
            }
            
            var prop = Utilities.CreateEntityByName<CBaseModelEntity>("planted_c4");

            if (prop == null) 
            {
                return false;
            }

            var playerOrigin = bombCarrier.PlayerPawn.Value!.AbsOrigin;

            if(playerOrigin == null)
            {
                return false;
            }

            playerOrigin.Z -= bombCarrier.PlayerPawn.Value.Collision.Mins.Z;

            prop.DispatchSpawn();

            CPlantedC4 plantedC4 = new CPlantedC4(prop.Handle);

            Server.NextFrame(() =>
            {
                prop.Teleport(playerOrigin, new QAngle(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), new Vector(0, 0, 0));

                gameRules.BombPlanted = true;

                // This works but it's not the best way to do it(questionable decision)
                plantedC4.BombTicking = true;

                this.SendBombPlantedEvent(bombCarrier);
                // this.RemoveC4FromCarrier(bombCarrier);
                
                Server.PrintToChatAll($"[{ChatColors.Red}Autoplant{ChatColors.Default}] {ChatColors.Green}Bomb has been planted!");
            });

            return true;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info) {
            var gameRules = GetGameRules();

            if(gameRules == null) 
            {
                return HookResult.Continue;
            }

            gameRules.BombPlanted = false;

            return HookResult.Continue;
        }

        public CCSPlayerController? GetBombCarrier()
        {
            CCSPlayerController? foundPlayer = null;

            foreach (var player in Utilities.GetPlayers())
            {
                if(player.PlayerPawn.Value!.WeaponServices == null) continue;
                
                foreach (var weapon in player.PlayerPawn.Value.WeaponServices.MyWeapons)
                {
                    if (weapon.Value == null) continue;
                    if (weapon.Value.DesignerName != "weapon_c4") continue;

                    foundPlayer = player;
                    break;
                }
            }

            return foundPlayer;
        }

        public bool RemoveC4FromCarrier(CCSPlayerController bombCarrier)
        {
            if(bombCarrier == null)
            {
                return false;
            }

            bombCarrier.PlayerPawn.Value!.WeaponServices!.MyWeapons!.Where(x => x.Value!.DesignerName == "weapon_c4").First().Value!.Remove();
            
            return true;
        }

        // Credits: killstr3ak
        public CCSGameRules GetGameRules()
        {
            return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules!;
        }

        public void SendBombPlantedEvent(CCSPlayerController bombCarrier) {
            if(bombCarrier.PlayerPawn.Value == null) {
                return;
            }

            var eventPtr = NativeAPI.CreateEvent("bomb_planted", true);
            NativeAPI.SetEventPlayerController(eventPtr, "userid", bombCarrier.Handle);
            NativeAPI.SetEventInt(eventPtr, "userid", (int)bombCarrier.PlayerPawn.Value.Index);

            NativeAPI.FireEvent(eventPtr, false);
        }
    }
}