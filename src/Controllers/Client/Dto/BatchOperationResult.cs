using System;
using System.Collections.Generic;

namespace GestionHogar.Controllers.Dtos;

public class BatchOperationResult
{
    public List<Guid> SuccessIds { get; set; } = new List<Guid>();
    public List<Guid> FailedIds { get; set; } = new List<Guid>();
    public int TotalProcessed => SuccessIds.Count + FailedIds.Count;
    public bool AllSucceeded => FailedIds.Count == 0 && SuccessIds.Count > 0;
}
