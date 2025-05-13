namespace TeiasMongoAPI.Services.DTOs.Response.Block
{
    public class ConcreteBlockResponseDto : BlockResponseDto
    {
        public double CompressiveStrengthOfConcrete { get; set; }
        public double YieldStrengthOfSteel { get; set; }
        public double TransverseReinforcementSpacing { get; set; }
        public double ReinforcementRatio { get; set; }
        public bool HookExists { get; set; }
        public bool IsStrengthened { get; set; }
    }
}