using Content.Server.Station.Components;
using JetBrains.Annotations;

namespace Content.Server.Station.Systems;
public sealed partial class StationJobsSystem
{
    public void MakeUnavailableJob(EntityUid station, string job, StationJobsComponent? stationJobs = null)
    {
        if (!Resolve(station, ref stationJobs))
            throw new ArgumentException("Tried to use a non-station entity as a station!", nameof(station));

        stationJobs.JobList[job] = 0;
        UpdateJobsAvailable();
    }
}
