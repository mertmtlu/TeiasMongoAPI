using MongoDB.Bson.Serialization.Attributes;

namespace TeiasMongoAPI.Core.Models.Block
{
    [BsonDiscriminator(RootClass = true)]
    [BsonKnownTypes(typeof(Concrete), typeof(Masonry))]
    public abstract class ABlock
    {
        public string ID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public abstract ModelingType ModelingType { get; }
        public double XAxisLength { get; set; }
        public double YAxisLength { get; set; }
        public required Dictionary<int, double> StoreyHeight { get; set; } = new();

        [BsonIgnore]
        public double LongLength
        {
            get
            {
                if (XAxisLength > YAxisLength) return XAxisLength;
                else return YAxisLength;
            }
        }
        [BsonIgnore]
        public double ShortLength
        {
            get
            {
                if (XAxisLength < YAxisLength) return XAxisLength;
                else return YAxisLength;
            }
        }
        [BsonIgnore]
        public double TotalHeight
        {
            get
            {
                double height = 0;
                foreach (var key in StoreyHeight.Keys) { if (!(key < 0)) height += StoreyHeight[key]; }
                return height;
            }
        }

        // TODO: Talk about areas
    }
}