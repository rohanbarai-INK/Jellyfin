using System;
using System.Collections.Generic;
using Jellyfin.Database.Implementations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Database.Providers.Sqlite.Migrations
{
    /// <summary>
    /// Creates the achievement definition table and seeds permanent achievements.
    /// </summary>
    [DbContext(typeof(JellyfinDbContext))]
    [Migration("20260302093000_AddAchievementDefinitions")]
    public partial class AddAchievementDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AchievementDefinition",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ImageEmoji = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Rarity = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Xp = table.Column<int>(type: "INTEGER", nullable: false),
                    Coins = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSeasonal = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AchievementDefinition", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AchievementDefinition_IsSeasonal",
                table: "AchievementDefinition",
                column: "IsSeasonal");

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in GetDefinitions())
            {
                if (!seenIds.Add(definition.Id))
                {
                    throw new InvalidOperationException($"Duplicate achievement id detected: {definition.Id}");
                }

                if (!IsKebabCase(definition.Id))
                {
                    throw new InvalidOperationException($"Achievement id must be kebab-case: {definition.Id}");
                }

                var rewards = GetRewardsByRarity(definition.Rarity);
                migrationBuilder.InsertData(
                    table: "AchievementDefinition",
                    columns: new[] { "Id", "Title", "Description", "ImageEmoji", "Rarity", "Xp", "Coins", "IsSeasonal" },
                    columnTypes: new[] { "TEXT", "TEXT", "TEXT", "TEXT", "TEXT", "INTEGER", "INTEGER", "INTEGER" },
                    values: new object[] { definition.Id, definition.Title, definition.Description, definition.ImageEmoji, definition.Rarity, rewards.Xp, rewards.Coins, false });
            }

            if (seenIds.Count != 100)
            {
                throw new InvalidOperationException($"Expected 100 achievement definitions but found {seenIds.Count}.");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AchievementDefinition");
        }

        private static (int Xp, int Coins) GetRewardsByRarity(string rarity)
            => rarity switch
            {
                "common" => (25, 5),
                "uncommon" => (75, 15),
                "rare" => (200, 40),
                "legendary" => (500, 100),
                _ => throw new InvalidOperationException($"Unsupported rarity value: {rarity}")
            };

        private static bool IsKebabCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (value.StartsWith('-')
                || value.EndsWith('-')
                || value.Contains("--", StringComparison.Ordinal))
            {
                return false;
            }

            foreach (var ch in value)
            {
                if ((ch is >= 'a' and <= 'z') || (ch is >= '0' and <= '9') || ch == '-')
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static IReadOnlyList<AchievementSeedDefinition> GetDefinitions()
            => new[]
            {
                // ONBOARDING
                new AchievementSeedDefinition("first-stream", "First Stream", "Played your first title.", "▶️", "common"),
                new AchievementSeedDefinition("episode-one", "Episode One", "Completed your first episode.", "📺", "common"),
                new AchievementSeedDefinition("feature-film", "Feature Film", "Completed your first movie.", "🎬", "common"),
                new AchievementSeedDefinition("first-request", "First Request", "Submitted your first content request.", "📝", "common"),
                new AchievementSeedDefinition("request-approved-first", "Request Approved", "Had your first request approved.", "✅", "common"),
                new AchievementSeedDefinition("weekend-viewer", "Weekend Viewer", "Watched content during the weekend.", "📆", "common"),
                new AchievementSeedDefinition("night-owl", "Night Owl", "Watched content after midnight.", "🌙", "common"),
                new AchievementSeedDefinition("early-bird", "Early Bird", "Watched content before 8 AM.", "🌅", "common"),
                new AchievementSeedDefinition("double-feature", "Double Feature", "Watched two titles in one day.", "🎞️", "common"),
                new AchievementSeedDefinition("platform-explorer", "Platform Explorer", "Browsed 10 different titles.", "🧭", "common"),

                // EPISODE PROGRESSION
                new AchievementSeedDefinition("five-episodes", "Five Episodes", "Completed 5 episodes.", "📺", "common"),
                new AchievementSeedDefinition("ten-episodes", "Ten Episodes", "Completed 10 episodes.", "📺", "common"),
                new AchievementSeedDefinition("twenty-episodes", "Twenty Episodes", "Completed 20 episodes.", "📺", "uncommon"),
                new AchievementSeedDefinition("fifty-episodes", "Fifty Episodes", "Completed 50 episodes.", "🔥", "rare"),
                new AchievementSeedDefinition("hundred-episodes", "Hundred Episodes", "Completed 100 episodes.", "🏆", "rare"),
                new AchievementSeedDefinition("binge-session", "Binge Session", "Watched 3 episodes in one sitting.", "🔥", "uncommon"),
                new AchievementSeedDefinition("mega-binge", "Mega Binge", "Watched 10 episodes in one day.", "🔥", "rare"),
                new AchievementSeedDefinition("season-finisher", "Season Finisher", "Completed an entire season.", "🏁", "uncommon"),
                new AchievementSeedDefinition("trilogy-night", "Trilogy Night", "Watched three feature-length titles in one night.", "🎬", "uncommon"),
                new AchievementSeedDefinition("cliffhanger-survivor", "Cliffhanger Survivor", "Started the next episode within 5 minutes.", "⏱️", "rare"),
                new AchievementSeedDefinition("midnight-marathon", "Midnight Marathon", "Completed multiple episodes after midnight.", "🌙", "uncommon"),
                new AchievementSeedDefinition("weekend-marathon", "Weekend Marathon", "Completed 8 episodes over a weekend.", "📆", "rare"),
                new AchievementSeedDefinition("back-to-back", "Back to Back", "Watched episodes on consecutive days.", "🔁", "uncommon"),
                new AchievementSeedDefinition("one-sitting", "One Sitting", "Finished a full season in one sitting.", "🕒", "rare"),
                new AchievementSeedDefinition("rewatcher", "Rewatcher", "Rewatched a full series from start to finish.", "🔄", "legendary"),

                // MOVIES
                new AchievementSeedDefinition("movie-buff", "Movie Buff", "Completed 5 movies.", "🎬", "common"),
                new AchievementSeedDefinition("cinema-lover", "Cinema Lover", "Completed 15 movies.", "🎥", "uncommon"),
                new AchievementSeedDefinition("film-collector", "Film Collector", "Completed 50 movies.", "📀", "rare"),
                new AchievementSeedDefinition("classic-viewer", "Classic Viewer", "Watched a classic film.", "🎞️", "rare"),
                new AchievementSeedDefinition("new-release", "New Release", "Watched a newly added title within 7 days.", "🆕", "rare"),
                new AchievementSeedDefinition("long-haul", "Long Haul", "Watched a movie longer than 3 hours.", "⏳", "rare"),
                new AchievementSeedDefinition("short-story", "Short Story", "Watched a movie under 90 minutes.", "🎬", "uncommon"),
                new AchievementSeedDefinition("double-movie-night", "Double Movie Night", "Watched two movies in one night.", "🎥", "uncommon"),
                new AchievementSeedDefinition("international-film", "International Film", "Watched a movie in a non-native language.", "🌍", "rare"),
                new AchievementSeedDefinition("documentary-dive", "Documentary Dive", "Completed 5 documentaries.", "📚", "uncommon"),

                // GENRE
                new AchievementSeedDefinition("action-fan", "Action Fan", "Watched your first action title.", "💥", "common"),
                new AchievementSeedDefinition("drama-enthusiast", "Drama Enthusiast", "Watched your first drama title.", "🎭", "common"),
                new AchievementSeedDefinition("comedy-club", "Comedy Club", "Watched your first comedy title.", "😂", "common"),
                new AchievementSeedDefinition("thriller-seeker", "Thriller Seeker", "Watched your first thriller title.", "🔪", "common"),
                new AchievementSeedDefinition("scifi-explorer", "Sci-Fi Explorer", "Watched your first science fiction title.", "👽", "common"),
                new AchievementSeedDefinition("romance-viewer", "Romance Viewer", "Watched your first romance title.", "💘", "common"),
                new AchievementSeedDefinition("horror-night", "Horror Night", "Watched horror content at night.", "😱", "common"),
                new AchievementSeedDefinition("mystery-mind", "Mystery Mind", "Completed a mystery title.", "🔍", "common"),
                new AchievementSeedDefinition("animation-watcher", "Animation Watcher", "Watched your first animation title.", "🎨", "common"),
                new AchievementSeedDefinition("fantasy-realm", "Fantasy Realm", "Watched your first fantasy title.", "🧙", "common"),
                new AchievementSeedDefinition("crime-analyst", "Crime Analyst", "Watched 5 crime titles.", "🕵️", "common"),
                new AchievementSeedDefinition("history-buff", "History Buff", "Watched 5 history titles.", "📜", "common"),
                new AchievementSeedDefinition("biography-viewer", "Biography Viewer", "Watched 3 biography titles.", "📖", "common"),
                new AchievementSeedDefinition("family-time", "Family Time", "Watched family content together.", "👨‍👩‍👧", "common"),
                new AchievementSeedDefinition("genre-loyalist", "Genre Loyalist", "Watched 10 titles from the same genre.", "🎯", "uncommon"),
                new AchievementSeedDefinition("genre-explorer", "Genre Explorer", "Watched titles across 8 different genres.", "🌈", "uncommon"),
                new AchievementSeedDefinition("balanced-viewer", "Balanced Viewer", "Watched at least 2 titles in 5 different genres.", "⚖️", "uncommon"),
                new AchievementSeedDefinition("global-explorer", "Global Explorer", "Watched content from 10 different countries.", "🌎", "uncommon"),
                new AchievementSeedDefinition("award-winner", "Award Winner", "Watched an award-winning title.", "🏆", "rare"),
                new AchievementSeedDefinition("critics-choice", "Critics Choice", "Watched 5 highly rated titles.", "⭐", "uncommon"),

                // REQUEST SYSTEM
                new AchievementSeedDefinition("request-pioneer", "Request Pioneer", "Submitted 3 content requests.", "📝", "common"),
                new AchievementSeedDefinition("request-regular", "Request Regular", "Submitted 10 content requests.", "📈", "uncommon"),
                new AchievementSeedDefinition("request-strategist", "Request Strategist", "Submitted requests across 3 genres.", "🎯", "uncommon"),
                new AchievementSeedDefinition("popular-choice", "Popular Choice", "Had a requested title watched by 5 users.", "👥", "common"),
                new AchievementSeedDefinition("curator", "Curator", "Maintained an active request list for 30 days.", "📂", "uncommon"),
                new AchievementSeedDefinition("content-contributor", "Content Contributor", "Had 10 requests approved.", "📊", "rare"),
                new AchievementSeedDefinition("trend-starter", "Trend Starter", "Started a request that became widely watched.", "🚀", "legendary"),
                new AchievementSeedDefinition("community-driver", "Community Driver", "Had multiple requests adopted by the community.", "👑", "rare"),
                new AchievementSeedDefinition("smart-spender", "Smart Spender", "Spent coins on your first request.", "🪙", "common"),
                new AchievementSeedDefinition("coin-collector", "Coin Collector", "Earned 100 total coins.", "💰", "common"),
                new AchievementSeedDefinition("coin-hoarder", "Coin Hoarder", "Saved 500 coins without spending.", "💎", "uncommon"),
                new AchievementSeedDefinition("high-roller", "High Roller", "Spent 300 coins on premium requests.", "💳", "uncommon"),
                new AchievementSeedDefinition("loyal-redeemer", "Loyal Redeemer", "Redeemed rewards for 30 consecutive days.", "🔄", "rare"),
                new AchievementSeedDefinition("boost-master", "Boost Master", "Used request boosts 20 times.", "⚡", "rare"),
                new AchievementSeedDefinition("strategic-planner", "Strategic Planner", "Timed requests to maximize approval success.", "🧠", "rare"),

                // SUBSCRIPTION
                new AchievementSeedDefinition("loyal-member", "Loyal Member", "Stayed subscribed for 3 consecutive months.", "🔄", "uncommon"),
                new AchievementSeedDefinition("dedicated-viewer", "Dedicated Viewer", "Stayed subscribed for 6 consecutive months.", "🛡️", "rare"),
                new AchievementSeedDefinition("year-one", "Year One", "Completed 12 months of subscription.", "🗓️", "rare"),
                new AchievementSeedDefinition("anniversary", "Anniversary", "Reached your subscription anniversary milestone.", "🎖️", "legendary"),
                new AchievementSeedDefinition("comeback", "Comeback", "Rejoined and resumed your subscription.", "🔁", "uncommon"),
                new AchievementSeedDefinition("continuous-supporter", "Continuous Supporter", "Maintained uninterrupted support for 18 months.", "🏅", "rare"),
                new AchievementSeedDefinition("early-renewal", "Early Renewal", "Renewed your subscription before expiry.", "⏳", "uncommon"),
                new AchievementSeedDefinition("premium-supporter", "Premium Supporter", "Maintained premium support for 12 months.", "👑", "rare"),
                new AchievementSeedDefinition("stability", "Stability", "Renewed on time for 6 billing cycles.", "📊", "uncommon"),
                new AchievementSeedDefinition("founding-member", "Founding Member", "Joined during the platform's early phase.", "🌟", "legendary"),

                // TIME
                new AchievementSeedDefinition("ten-hours", "Ten Hours", "Reached 10 total watch hours.", "⏳", "common"),
                new AchievementSeedDefinition("fifty-hours", "Fifty Hours", "Reached 50 total watch hours.", "⏱️", "uncommon"),
                new AchievementSeedDefinition("hundred-hours", "Hundred Hours", "Reached 100 total watch hours.", "🕒", "rare"),
                new AchievementSeedDefinition("two-fifty-hours", "250 Hours", "Reached 250 total watch hours.", "⌛", "rare"),
                new AchievementSeedDefinition("five-hundred-hours", "500 Hours", "Reached 500 total watch hours.", "🏆", "legendary"),
                new AchievementSeedDefinition("daily-viewer", "Daily Viewer", "Watched content every day for 7 days.", "📆", "common"),
                new AchievementSeedDefinition("weekly-habit", "Weekly Habit", "Watched content every week for 8 weeks.", "📅", "uncommon"),
                new AchievementSeedDefinition("monthly-active", "Monthly Active", "Watched content every month for 12 months.", "📊", "legendary"),
                new AchievementSeedDefinition("comeback-king", "Comeback King", "Returned after inactivity and watched again.", "👑", "common"),
                new AchievementSeedDefinition("prime-time", "Prime Time", "Watched consistently during prime evening hours.", "🌆", "uncommon"),

                // PRESTIGE
                new AchievementSeedDefinition("completionist", "Completionist", "Completed 100 titles.", "📺", "rare"),
                new AchievementSeedDefinition("master-viewer", "Master Viewer", "Completed 250 titles.", "🏆", "rare"),
                new AchievementSeedDefinition("elite-curator", "Elite Curator", "Had 25 approved content requests.", "👑", "legendary"),
                new AchievementSeedDefinition("ultimate-binger", "Ultimate Binger", "Completed 20 binge sessions.", "🔥", "rare"),
                new AchievementSeedDefinition("cinematic-scholar", "Cinematic Scholar", "Completed titles across all major genres.", "🎓", "legendary"),
                new AchievementSeedDefinition("genre-master", "Genre Master", "Reached advanced milestones in 10 genres.", "🧠", "rare"),
                new AchievementSeedDefinition("global-cinema", "Global Cinema", "Watched 100 international titles.", "🌍", "legendary"),
                new AchievementSeedDefinition("platform-veteran", "Platform Veteran", "Stayed active on the platform for 3 years.", "🏅", "legendary"),
                new AchievementSeedDefinition("legend", "Legend", "Unlocked 90 achievements.", "💎", "legendary"),
                new AchievementSeedDefinition("immortal-viewer", "Immortal Viewer", "Reached Level 100.", "👑", "legendary")
            };

        private readonly record struct AchievementSeedDefinition(
            string Id,
            string Title,
            string Description,
            string ImageEmoji,
            string Rarity);
    }
}
