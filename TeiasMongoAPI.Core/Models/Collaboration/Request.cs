using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TeiasMongoAPI.Core.Models.Base;
using System;
using System.Collections.Generic;

namespace TeiasMongoAPI.Core.Models.Collaboration
{
    public class Request : AEntityBase
    {
        public string Type { get; set; } = string.Empty;  // feature, ui, review

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public ObjectId? ProgramId { get; set; }  // Optional, program-specific request

        public string RequestedBy { get; set; } = string.Empty;  // User ID

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        public string? AssignedTo { get; set; }  // Admin User ID

        public string Status { get; set; } = "open";  // open, in_progress, completed, rejected

        public string Priority { get; set; } = "normal";  // low, normal, high

        public List<RequestResponse> Responses { get; set; } = new List<RequestResponse>();

        public object Metadata { get; set; } = new object();  // Request-specific data
        
        public ObjectId? RelatedEntityId { get; set; }

        public string RelatedEntityType { get; set; } = string.Empty;
    }

    public class RequestResponse
    {
        public string RespondedBy { get; set; } = string.Empty;  // User ID

        public DateTime RespondedAt { get; set; } = DateTime.UtcNow;

        public string Message { get; set; } = string.Empty;
    }
}