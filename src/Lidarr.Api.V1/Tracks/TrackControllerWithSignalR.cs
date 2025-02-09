using System.Collections.Generic;
using Lidarr.Api.V1.Artist;
using Lidarr.Api.V1.TrackFiles;
using Lidarr.Http.REST;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Music;
using NzbDrone.SignalR;

namespace Lidarr.Api.V1.Tracks
{
    public abstract class TrackControllerWithSignalR : RestControllerWithSignalR<TrackResource, Track>,
            IHandle<TrackImportedEvent>,
            IHandle<TrackFileDeletedEvent>
    {
        protected readonly ITrackService _trackService;
        protected readonly IArtistService _artistService;
        protected readonly IUpgradableSpecification _upgradableSpecification;
        private readonly ICustomFormatCalculationService _formatCalculator;

        protected TrackControllerWithSignalR(ITrackService trackService,
                                           IArtistService artistService,
                                           IUpgradableSpecification upgradableSpecification,
                                           ICustomFormatCalculationService formatCalculator,
                                           IBroadcastSignalRMessage signalRBroadcaster)
            : base(signalRBroadcaster)
        {
            _trackService = trackService;
            _artistService = artistService;
            _upgradableSpecification = upgradableSpecification;
            _formatCalculator = formatCalculator;
        }

        public override TrackResource GetResourceById(int id)
        {
            var track = _trackService.GetTrack(id);
            var resource = MapToResource(track, true, true);
            return resource;
        }

        protected TrackResource MapToResource(Track track, bool includeArtist, bool includeTrackFile)
        {
            var resource = track.ToResource();

            if (includeArtist || includeTrackFile)
            {
                var artist = track.Artist.Value;

                if (includeArtist)
                {
                    resource.Artist = artist.ToResource();
                }

                if (includeTrackFile && track.TrackFileId != 0)
                {
                    resource.TrackFile = track.TrackFile.Value.ToResource(artist, _upgradableSpecification, _formatCalculator);
                }
            }

            return resource;
        }

        protected List<TrackResource> MapToResource(List<Track> tracks, bool includeArtist, bool includeTrackFile)
        {
            var result = tracks.ToResource();

            if (includeArtist || includeTrackFile)
            {
                var artistDict = new Dictionary<int, NzbDrone.Core.Music.Artist>();
                for (var i = 0; i < tracks.Count; i++)
                {
                    var track = tracks[i];
                    var resource = result[i];
                    var artist = track.Artist.Value;

                    if (includeArtist)
                    {
                        resource.Artist = artist.ToResource();
                    }

                    if (includeTrackFile && tracks[i].TrackFileId != 0)
                    {
                        resource.TrackFile = tracks[i].TrackFile.Value.ToResource(artist, _upgradableSpecification, _formatCalculator);
                    }
                }
            }

            return result;
        }

        [NonAction]
        public void Handle(TrackImportedEvent message)
        {
            foreach (var track in message.TrackInfo.Tracks)
            {
                track.TrackFile = message.ImportedTrack;
                BroadcastResourceChange(ModelAction.Updated, MapToResource(track, true, true));
            }
        }

        [NonAction]
        public void Handle(TrackFileDeletedEvent message)
        {
            foreach (var track in message.TrackFile.Tracks.Value)
            {
                track.TrackFile = message.TrackFile;
                BroadcastResourceChange(ModelAction.Updated, MapToResource(track, true, true));
            }
        }
    }
}
