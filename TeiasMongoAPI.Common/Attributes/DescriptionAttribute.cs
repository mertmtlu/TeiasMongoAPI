using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class PropertyDescriptionAttribute : Attribute
    {
        public string Description { get; }
        public string? Unit { get; }
        public string? Example { get; }

        public PropertyDescriptionAttribute(string description, string? unit = null, string? example = null)
        {
            Description = description;
            Unit = unit;
            Example = example;
        }
    }
}