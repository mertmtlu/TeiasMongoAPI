using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Core.Models.Collaboration
{
    public static class ProgramConstants
    {
        public static class UiTypes
        {
            public const string Console = "console";
            public const string Desktop = "desktop";
            public const string Web = "web";
            public const string Custom = "custom";

            // New options
            public const string PreBuiltApp = "prebuilt_app";
            public const string StaticSite = "static_site";
            public const string MicroFrontend = "micro_frontend";
            public const string ContainerApp = "container_app";
        }

        public static class Languages
        {
            public const string Python = "python";
            public const string CSharp = "csharp";
            public const string Rust = "rust";
            public const string CPP = "cpp";
            public const string Java = "java";

            // New options
            public const string Angular = "angular";
            public const string React = "react";
            public const string Vue = "vue";
            public const string Svelte = "svelte";
            public const string Html = "html";
            public const string Docker = "docker";
        }
    }
}
