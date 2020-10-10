﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using TMDbLib.Objects.Authentication;
using TMDbLib.Objects.Changes;
using TMDbLib.Objects.General;
using TMDbLib.Objects.TvShows;
using TMDbLibTests.Helpers;
using TMDbLibTests.JsonHelpers;
using Credits = TMDbLib.Objects.TvShows.Credits;

namespace TMDbLibTests
{
    public class ClientTvSeasonTests : TestBase
    {
        private static Dictionary<TvSeasonMethods, Func<TvSeason, object>> _methods;

        public ClientTvSeasonTests()
        {
            _methods = new Dictionary<TvSeasonMethods, Func<TvSeason, object>>
            {
                [TvSeasonMethods.Credits] = tvSeason => tvSeason.Credits,
                [TvSeasonMethods.Images] = tvSeason => tvSeason.Images,
                [TvSeasonMethods.ExternalIds] = tvSeason => tvSeason.ExternalIds,
                [TvSeasonMethods.Videos] = tvSeason => tvSeason.Videos,
                [TvSeasonMethods.Videos] = tvSeason => tvSeason.Videos,
                [TvSeasonMethods.AccountStates] = tvSeason => tvSeason.AccountStates
            };
        }

        [Fact]
        public async Task TestTvSeasonExtrasNoneAsync()
        {
            TvSeason tvSeason = await Config.Client.GetTvSeasonAsync(IdHelper.BreakingBad, 1);

            TestBreakingBadBaseProperties(tvSeason);

            // Test all extras, ensure none of them are populated
            foreach (Func<TvSeason, object> selector in _methods.Values)
            {
                Assert.Null(selector(tvSeason));
            }
        }

        [Fact]
        public async Task TestTvSeasonExtrasAccountState()
        {
            // Test the custom parsing code for Account State rating
            await Config.Client.SetSessionInformationAsync(Config.UserSessionId, SessionType.UserSession);

            TvSeason season = await Config.Client.GetTvSeasonAsync(IdHelper.BigBangTheory, 1, TvSeasonMethods.AccountStates);
            if (season.AccountStates == null || season.AccountStates.Results.All(s => s.EpisodeNumber != 1))
            {
                await Config.Client.TvEpisodeSetRatingAsync(IdHelper.BigBangTheory, 1, 1, 5);

                // Allow TMDb to update cache
                Thread.Sleep(2000);

                season = await Config.Client.GetTvSeasonAsync(IdHelper.BigBangTheory, 1, TvSeasonMethods.AccountStates);
            }

            Assert.NotNull(season.AccountStates);
            Assert.True(season.AccountStates.Results.Single(s => s.EpisodeNumber == 1).Rating.HasValue);
            Assert.True(Math.Abs(season.AccountStates.Results.Single(s => s.EpisodeNumber == 1).Rating.Value - 5) < double.Epsilon);
        }

        [Fact]
        public async Task TestTvSeasonExtrasAllAsync()
        {
            await Config.Client.SetSessionInformationAsync(Config.UserSessionId, SessionType.UserSession);

            // Account states will only show up if we've done something
            await Config.Client.TvEpisodeSetRatingAsync(IdHelper.BreakingBad, 1, 1, 5);

            await TestMethodsHelper.TestGetAll(_methods, combined => Config.Client.GetTvSeasonAsync(IdHelper.BreakingBad, 1, combined), TestBreakingBadBaseProperties);
        }

        [Fact]
        public async Task TestTvSeasonExtrasExclusiveAsync()
        {
            await Config.Client.SetSessionInformationAsync(Config.UserSessionId, SessionType.UserSession);
            await TestMethodsHelper.TestGetExclusive(_methods, extras => Config.Client.GetTvSeasonAsync(IdHelper.BreakingBad, 1, extras));
        }

        [Fact]
        public async Task TestTvSeasonSeparateExtrasCreditsAsync()
        {
            Credits credits = await Config.Client.GetTvSeasonCreditsAsync(IdHelper.BreakingBad, 1);
            Assert.NotNull(credits);
            Assert.NotNull(credits.Cast);
            Assert.Equal("Walter White", credits.Cast[0].Character);
            Assert.Equal("52542282760ee313280017f9", credits.Cast[0].CreditId);
            Assert.Equal(17419, credits.Cast[0].Id);
            Assert.Equal("Bryan Cranston", credits.Cast[0].Name);
            Assert.NotNull(credits.Cast[0].ProfilePath);
            Assert.Equal(0, credits.Cast[0].Order);

            Crew crewPersonId = credits.Crew.FirstOrDefault(s => s.Id == 1223202);
            Assert.NotNull(crewPersonId);

            Assert.Equal(1223202, crewPersonId.Id);
            Assert.Equal("Production", crewPersonId.Department);
            Assert.Equal("Diane Mercer", crewPersonId.Name);
            Assert.Equal("Producer", crewPersonId.Job);
            Assert.Null(crewPersonId.ProfilePath);
        }

        [Fact]
        public async Task TestTvSeasonSeparateExtrasExternalIdsAsync()
        {
            ExternalIdsTvSeason externalIds = await Config.Client.GetTvSeasonExternalIdsAsync(IdHelper.BreakingBad, 1);

            Assert.NotNull(externalIds);
            Assert.Equal(3572, externalIds.Id);
            Assert.Equal("/en/breaking_bad_season_1", externalIds.FreebaseId);
            Assert.Equal("/m/05yy27m", externalIds.FreebaseMid);
            Assert.Null(externalIds.TvrageId);
            Assert.Equal("30272", externalIds.TvdbId);
        }

        [Fact]
        public async Task TestTvSeasonSeparateExtrasImagesAsync()
        {
            PosterImages images = await Config.Client.GetTvSeasonImagesAsync(IdHelper.BreakingBad, 1);
            Assert.NotNull(images);
            Assert.NotNull(images.Posters);
        }

        [Fact]
        public async Task TestTvSeasonSeparateExtrasVideosAsync()
        {
            ResultContainer<Video> videos = await Config.Client.GetTvSeasonVideosAsync(IdHelper.BreakingBad, 1);
            Assert.NotNull(videos);
            Assert.NotNull(videos.Results);
        }

        [Fact]
        public async Task TestTvSeasonAccountStateRatingSetAsync()
        {
            await Config.Client.SetSessionInformationAsync(Config.UserSessionId, SessionType.UserSession);

            // Rate episode 1, 2 and 3 of BreakingBad
            Assert.True(await Config.Client.TvEpisodeSetRatingAsync(IdHelper.BreakingBad, 1, 1, 5));
            Assert.True(await Config.Client.TvEpisodeSetRatingAsync(IdHelper.BreakingBad, 1, 2, 7));
            Assert.True(await Config.Client.TvEpisodeSetRatingAsync(IdHelper.BreakingBad, 1, 3, 3));

            // Wait for TMDb to un-cache our value
            Thread.Sleep(2000);

            // Fetch out the seasons state
            ResultContainer<TvEpisodeAccountStateWithNumber> state = await Config.Client.GetTvSeasonAccountStateAsync(IdHelper.BreakingBad, 1);
            Assert.NotNull(state);

            Assert.True(Math.Abs(5 - (state.Results.Single(s => s.EpisodeNumber == 1).Rating ?? 0)) < double.Epsilon);
            Assert.True(Math.Abs(7 - (state.Results.Single(s => s.EpisodeNumber == 2).Rating ?? 0)) < double.Epsilon);
            Assert.True(Math.Abs(3 - (state.Results.Single(s => s.EpisodeNumber == 3).Rating ?? 0)) < double.Epsilon);

            // Test deleting Ratings
            Assert.True(await Config.Client.TvEpisodeRemoveRatingAsync(IdHelper.BreakingBad, 1, 1));
            Assert.True(await Config.Client.TvEpisodeRemoveRatingAsync(IdHelper.BreakingBad, 1, 2));
            Assert.True(await Config.Client.TvEpisodeRemoveRatingAsync(IdHelper.BreakingBad, 1, 3));

            // Wait for TMDb to un-cache our value
            Thread.Sleep(2000);

            state = await Config.Client.GetTvSeasonAccountStateAsync(IdHelper.BreakingBad, 1);
            Assert.NotNull(state);

            Assert.Null(state.Results.Single(s => s.EpisodeNumber == 1).Rating);
            Assert.Null(state.Results.Single(s => s.EpisodeNumber == 2).Rating);
            Assert.Null(state.Results.Single(s => s.EpisodeNumber == 3).Rating);
        }

        [Fact]
        public async Task TestTvSeasonGetChangesAsync()
        {
            ChangesContainer changes = await Config.Client.GetTvSeasonChangesAsync(IdHelper.BreakingBadSeason1Id);
            Assert.NotNull(changes);
            Assert.NotNull(changes.Changes);
        }

        private void TestBreakingBadBaseProperties(TvSeason tvSeason)
        {
            Assert.NotNull(tvSeason);
            Assert.NotNull(tvSeason.Id);
            Assert.Equal(1, tvSeason.SeasonNumber);
            Assert.Equal("Season 1", tvSeason.Name);
            Assert.NotNull(tvSeason.AirDate);
            Assert.NotNull(tvSeason.Overview);
            Assert.NotNull(tvSeason.PosterPath);

            Assert.NotNull(tvSeason.Episodes);
            Assert.Equal(7, tvSeason.Episodes.Count);
            Assert.Equal(1, tvSeason.Episodes[0].EpisodeNumber);
            Assert.Equal("Pilot", tvSeason.Episodes[0].Name);
            Assert.NotNull(tvSeason.Episodes[0].Overview);
            Assert.Null(tvSeason.Episodes[0].ProductionCode);
            Assert.Equal(1, tvSeason.Episodes[0].SeasonNumber);
            Assert.NotNull(tvSeason.Episodes[0].StillPath);
        }

        [Fact]
        public async Task TestTvSeasonMissingAsync()
        {
            TvSeason tvSeason = await Config.Client.GetTvSeasonAsync(IdHelper.MissingID, 1);

            Assert.Null(tvSeason);
        }

        [Fact]
        public async Task TestTvSeasonGetTvSeasonWithImageLanguageAsync()
        {
            TvSeason resp = await Config.Client.GetTvSeasonAsync(IdHelper.BreakingBad, 1, language: "en-US", includeImageLanguage: "en", extraMethods: TvSeasonMethods.Images);

            Assert.True(resp.Images.Posters.Count > 0);
            Assert.True(resp.Images.Posters.All(p => p.Iso_639_1.Equals("en", StringComparison.OrdinalIgnoreCase)));
        }
    }
}