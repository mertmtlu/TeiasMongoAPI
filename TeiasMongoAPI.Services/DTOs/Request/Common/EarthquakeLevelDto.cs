namespace TeiasMongoAPI.Services.DTOs.Request.Common
{
    public class EarthquakeLevelDto
    {
        public double PGA { get; set; }
        public double PGV { get; set; }
        public double Ss { get; set; }
        public double S1 { get; set; }
        public double Sds { get; set; }
        public double Sd1 { get; set; }
    }
}