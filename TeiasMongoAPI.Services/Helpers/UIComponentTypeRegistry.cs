using System;
using System.Collections.Generic;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;

namespace TeiasMongoAPI.Services.Helpers
{
    public static class UIComponentTypeRegistry
    {
        private static readonly Dictionary<string, Type> TypeMap = new()
        {
            // Simple Types
            { "text_input", typeof(string) },
            { "email_input", typeof(string) },
            { "password_input", typeof(string) },
            { "textarea", typeof(string) },
            { "number_input", typeof(double?) },
            { "dropdown", typeof(string) },
            { "checkbox", typeof(bool) },
            { "radio", typeof(string) },
            { "date_picker", typeof(string) }, // Corresponds to date_input
            { "date_input", typeof(string) },
            { "slider", typeof(double?) },
            { "multi_select", typeof(List<string>) },

            // Complex DTO Types
            { "map_input", typeof(List<NamedPointDto>) }, // Note: This is a List to handle multiple points
            { "file_input", typeof(List<FileDataDto>) }, // Using a List to support multiple file uploads

            // Generic/Fallback Types
            { "table", typeof(Dictionary<string, object>) },
            { "button", typeof(object) }, // Buttons typically don't have a data value
            { "label", typeof(object) },  // Labels don't have a data value
        };

        public static Type GetTypeForElement(string elementType)
        {
            // Fallback to a generic object for any unknown types to prevent errors
            return TypeMap.GetValueOrDefault(elementType, typeof(object));
        }
    }
}