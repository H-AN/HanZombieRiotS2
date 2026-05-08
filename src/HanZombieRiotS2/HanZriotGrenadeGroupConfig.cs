namespace HanZombieRiotS2;

public class HanZriotGrenadeGroupConfig
{
    public FireGrenadeConfig FireGrenade { get; set; } = new();
    public LightGrenadeConfig LightGrenade { get; set; } = new();
    public FreezeGrenadeConfig FreezeGrenade { get; set; } = new();

    public abstract class GrenadeRuleConfig
    {
        public bool Enabled { get; set; } = false;
        public bool GiveOnSpawn { get; set; } = false;
        public bool AllowBots { get; set; } = false;
        public int RoundLimit { get; set; } = 0;
        public int LifeLimit { get; set; } = 0;
        public string Sound { get; set; } = string.Empty;
        public string PrecacheSoundEvent { get; set; } = string.Empty;
    }

    public sealed class FireGrenadeConfig : GrenadeRuleConfig
    {
        public float ExplosionDamage { get; set; } = 0f;
        public float ExplosionRadius { get; set; } = 300f;
        public float BurnDamage { get; set; } = 0f;
        public float BurnDuration { get; set; } = 0f;
        public string BurnParticle { get; set; } = "particles/burning_fx/env_fire_large.vpcf";
        public string BurnSound { get; set; } = string.Empty;
    }

    public sealed class LightGrenadeConfig : GrenadeRuleConfig
    {
        public float Duration { get; set; } = 10f;
        public float LightRadius { get; set; } = 800f;
        public float Brightness { get; set; } = 5f;
    }

    public sealed class FreezeGrenadeConfig : GrenadeRuleConfig
    {
        public float FreezeRadius { get; set; } = 300f;
        public float FreezeDuration { get; set; } = 4f;
        public string FreezeSound { get; set; } = string.Empty;
        public string UnfreezeSound { get; set; } = string.Empty;
    }
}
