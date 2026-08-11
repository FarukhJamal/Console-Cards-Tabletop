using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ConsoleCards.Core.Domain;
using ConsoleCards.Core.Domain.Containers;
using ConsoleCards.Core.Domain.Match;
using ConsoleCards.Core.Domain.PlayAreas;
using ConsoleCards.Core.Domain.PlayerLayouts;
using ConsoleCards.Core.Domain.Seats;
using ConsoleCards.Core.Identifiers;

namespace ConsoleCards.GameTemplates
{
    public enum TabletopSessionKind
    {
        EmptyCustom = 0,
        GameTemplate = 1,
    }

    /// <summary>
    /// Stable, UI-independent identification of the requested session setup.
    /// </summary>
    public readonly struct TabletopSessionSelection : IEquatable<TabletopSessionSelection>
    {
        private TabletopSessionSelection(TabletopSessionKind kind, GameTemplateId gameTemplateId)
        {
            Kind = kind;
            GameTemplateId = gameTemplateId;
        }

        public TabletopSessionKind Kind { get; }

        public GameTemplateId GameTemplateId { get; }

        public static TabletopSessionSelection EmptyCustom =>
            new TabletopSessionSelection(TabletopSessionKind.EmptyCustom, GameTemplateId.Empty);

        public static TabletopSessionSelection FromGameTemplate(GameTemplateId gameTemplateId)
        {
            if (gameTemplateId.IsEmpty)
            {
                throw new ArgumentException("A Game Template session requires a non-empty Template ID.", nameof(gameTemplateId));
            }

            return new TabletopSessionSelection(TabletopSessionKind.GameTemplate, gameTemplateId);
        }

        public bool Equals(TabletopSessionSelection other)
        {
            return Kind == other.Kind && GameTemplateId == other.GameTemplateId;
        }

        public override bool Equals(object obj)
        {
            return obj is TabletopSessionSelection other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Kind.GetHashCode() * 397) ^ GameTemplateId.GetHashCode();
        }

        public static bool operator ==(TabletopSessionSelection left, TabletopSessionSelection right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TabletopSessionSelection left, TabletopSessionSelection right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Actor-aware request that can later be supplied by a lobby/host without changing session construction.
    /// </summary>
    public sealed class TabletopSessionBootstrapRequest
    {
        private readonly ReadOnlyCollection<PlayerId> activePlayerIds;

        public TabletopSessionBootstrapRequest(
            PlayerId requestingPlayerId,
            TabletopSessionSelection selection,
            IEnumerable<PlayerId> activePlayerIds,
            MatchId matchId)
        {
            if (activePlayerIds == null)
            {
                throw new ArgumentNullException(nameof(activePlayerIds));
            }

            RequestingPlayerId = requestingPlayerId;
            Selection = selection;
            this.activePlayerIds = new ReadOnlyCollection<PlayerId>(new List<PlayerId>(activePlayerIds));
            MatchId = matchId;
        }

        public PlayerId RequestingPlayerId { get; }

        public TabletopSessionSelection Selection { get; }

        public IReadOnlyList<PlayerId> ActivePlayerIds => activePlayerIds;

        public MatchId MatchId { get; }
    }

    public sealed class GameTemplateRegistration
    {
        public GameTemplateRegistration(GameTemplate template, GameTemplateContentCatalog contentCatalog)
        {
            Template = template ?? throw new ArgumentNullException(nameof(template));
            ContentCatalog = contentCatalog ?? throw new ArgumentNullException(nameof(contentCatalog));
        }

        public GameTemplate Template { get; }

        public GameTemplateContentCatalog ContentCatalog { get; }
    }

    /// <summary>
    /// Minimum in-memory catalog used by Session Entry. It catalogs setup data, not Game rules.
    /// </summary>
    public sealed class GameTemplateCatalog
    {
        private readonly ReadOnlyDictionary<GameTemplateId, GameTemplateRegistration> registrations;

        public GameTemplateCatalog(IEnumerable<GameTemplateRegistration> registrations)
        {
            if (registrations == null)
            {
                throw new ArgumentNullException(nameof(registrations));
            }

            Dictionary<GameTemplateId, GameTemplateRegistration> indexed =
                new Dictionary<GameTemplateId, GameTemplateRegistration>();
            foreach (GameTemplateRegistration registration in registrations)
            {
                if (registration == null)
                {
                    throw new ArgumentException("Game Template registrations cannot contain null entries.", nameof(registrations));
                }

                GameTemplateId id = registration.Template.Id;
                if (id.IsEmpty)
                {
                    throw new ArgumentException("Registered Game Templates require non-empty IDs.", nameof(registrations));
                }

                if (indexed.ContainsKey(id))
                {
                    throw new ArgumentException("Registered Game Template IDs must be unique.", nameof(registrations));
                }

                indexed.Add(id, registration);
            }

            this.registrations = new ReadOnlyDictionary<GameTemplateId, GameTemplateRegistration>(indexed);
        }

        public IReadOnlyDictionary<GameTemplateId, GameTemplateRegistration> Registrations => registrations;

        public bool TryGet(GameTemplateId id, out GameTemplateRegistration registration)
        {
            return registrations.TryGetValue(id, out registration);
        }
    }

    public sealed class TabletopSessionBootstrapIssue
    {
        public TabletopSessionBootstrapIssue(string code, string message)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public string Code { get; }

        public string Message { get; }

        public override string ToString()
        {
            return $"{Code}: {Message}";
        }
    }

    public sealed class TabletopSessionBootstrapResult
    {
        private readonly ReadOnlyCollection<TabletopSessionBootstrapIssue> issues;

        private TabletopSessionBootstrapResult(
            TabletopSession session,
            IEnumerable<TabletopSessionBootstrapIssue> issues)
        {
            Session = session;
            this.issues = new ReadOnlyCollection<TabletopSessionBootstrapIssue>(
                new List<TabletopSessionBootstrapIssue>(issues));
        }

        public bool Succeeded => Session != null;

        public TabletopSession Session { get; }

        public IReadOnlyList<TabletopSessionBootstrapIssue> Issues => issues;

        internal static TabletopSessionBootstrapResult Success(TabletopSession session)
        {
            return new TabletopSessionBootstrapResult(
                session ?? throw new ArgumentNullException(nameof(session)),
                Array.Empty<TabletopSessionBootstrapIssue>());
        }

        internal static TabletopSessionBootstrapResult Failure(
            IEnumerable<TabletopSessionBootstrapIssue> issues)
        {
            return new TabletopSessionBootstrapResult(null, issues);
        }
    }

    /// <summary>
    /// Owns exactly one authoritative local Match and its reset path, regardless of entry source.
    /// </summary>
    public sealed class TabletopSession
    {
        private readonly GameTemplateMatchSession gameTemplateSession;
        private readonly GameTemplateInitialSnapshot emptyTableBaseline;

        internal TabletopSession(
            TabletopSessionBootstrapRequest request,
            MatchState emptyTableMatch,
            GameTemplateInitialSnapshot emptyTableBaseline)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            CurrentMatch = emptyTableMatch ?? throw new ArgumentNullException(nameof(emptyTableMatch));
            this.emptyTableBaseline = emptyTableBaseline
                ?? throw new ArgumentNullException(nameof(emptyTableBaseline));
        }

        internal TabletopSession(
            TabletopSessionBootstrapRequest request,
            GameTemplateMatchSession gameTemplateSession)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            this.gameTemplateSession = gameTemplateSession
                ?? throw new ArgumentNullException(nameof(gameTemplateSession));
            CurrentMatch = gameTemplateSession.CurrentMatch;
        }

        public TabletopSessionBootstrapRequest Request { get; }

        public TabletopSessionSelection Selection => Request.Selection;

        public MatchState CurrentMatch { get; private set; }

        public GameTemplate Template => gameTemplateSession?.Template;

        public PlayerLayoutDefinition PlayerLayout => gameTemplateSession?.PlayerLayout;

        public IReadOnlyList<GameTemplateCameraBookmarkDefinition> CameraBookmarks =>
            gameTemplateSession != null
                ? gameTemplateSession.CameraBookmarks
                : Array.Empty<GameTemplateCameraBookmarkDefinition>();

        public MatchState Reset()
        {
            MatchState replacement = gameTemplateSession != null
                ? gameTemplateSession.Reset()
                : emptyTableBaseline.Restore();
            CurrentMatch = replacement;
            return replacement;
        }
    }

    /// <summary>
    /// Validates a Session Entry request before exposing an authoritative local session.
    /// </summary>
    public sealed class TabletopSessionBootstrapService
    {
        public TabletopSessionBootstrapResult TryCreate(
            TabletopSessionBootstrapRequest request,
            GameTemplateCatalog gameTemplateCatalog)
        {
            List<TabletopSessionBootstrapIssue> issues = ValidateRequest(request);
            if (request == null)
            {
                return TabletopSessionBootstrapResult.Failure(issues);
            }

            GameTemplateRegistration registration = null;
            if (request.Selection.Kind == TabletopSessionKind.GameTemplate)
            {
                if (gameTemplateCatalog == null)
                {
                    Add(issues, "TemplateCatalogRequired", "Game Template selection requires a Template catalog.");
                }
                else if (!gameTemplateCatalog.TryGet(request.Selection.GameTemplateId, out registration))
                {
                    Add(issues, "TemplateNotRegistered", "The selected Game Template is not registered for Session Entry.");
                }
            }

            if (issues.Count > 0)
            {
                return TabletopSessionBootstrapResult.Failure(issues);
            }

            try
            {
                if (request.Selection.Kind == TabletopSessionKind.EmptyCustom)
                {
                    MatchState match = new MatchState(
                        request.MatchId,
                        GameTemplateId.Empty,
                        0,
                        Array.Empty<CardInstanceState>(),
                        Array.Empty<PawnState>(),
                        Array.Empty<TokenState>(),
                        Array.Empty<ContainerState>(),
                        Array.Empty<SeatState>(),
                        Array.Empty<ContainerPlacementState>(),
                        Array.Empty<PlayAreaState>());
                    GameTemplateInitialSnapshot baseline = GameTemplateInitialSnapshot.Capture(match);
                    return TabletopSessionBootstrapResult.Success(
                        new TabletopSession(request, match, baseline));
                }

                GameTemplateMatchBuildResult templateResult = new GameTemplateMatchFactory().TryCreate(
                    registration.Template,
                    registration.ContentCatalog,
                    request.ActivePlayerIds,
                    request.MatchId);
                if (!templateResult.Succeeded)
                {
                    List<TabletopSessionBootstrapIssue> templateIssues =
                        new List<TabletopSessionBootstrapIssue>(templateResult.Issues.Count);
                    for (int i = 0; i < templateResult.Issues.Count; i++)
                    {
                        GameTemplateValidationIssue issue = templateResult.Issues[i];
                        templateIssues.Add(new TabletopSessionBootstrapIssue(issue.Code, issue.Message));
                    }

                    return TabletopSessionBootstrapResult.Failure(templateIssues);
                }

                return TabletopSessionBootstrapResult.Success(
                    new TabletopSession(request, templateResult.Session));
            }
            catch (ArgumentException exception)
            {
                return ConstructionFailure(exception);
            }
            catch (InvalidOperationException exception)
            {
                return ConstructionFailure(exception);
            }
            catch (KeyNotFoundException exception)
            {
                return ConstructionFailure(exception);
            }
            catch (OverflowException exception)
            {
                return ConstructionFailure(exception);
            }
        }

        private static List<TabletopSessionBootstrapIssue> ValidateRequest(
            TabletopSessionBootstrapRequest request)
        {
            List<TabletopSessionBootstrapIssue> issues = new List<TabletopSessionBootstrapIssue>();
            if (request == null)
            {
                Add(issues, "SessionRequestRequired", "A Session Entry request is required.");
                return issues;
            }

            if (request.RequestingPlayerId.IsEmpty)
            {
                Add(issues, "RequestingPlayerRequired", "Session Entry requires an initiating Player identity.");
            }

            if (request.MatchId.IsEmpty)
            {
                Add(issues, "MatchIdEmpty", "Session Entry requires a non-empty Match ID.");
            }

            if (!Enum.IsDefined(typeof(TabletopSessionKind), request.Selection.Kind))
            {
                Add(issues, "SessionKindInvalid", "The selected session kind is invalid.");
            }
            else if (request.Selection.Kind == TabletopSessionKind.EmptyCustom
                && !request.Selection.GameTemplateId.IsEmpty)
            {
                Add(issues, "EmptySessionTemplateInvalid", "An Empty/Custom session cannot reference a Game Template.");
            }
            else if (request.Selection.Kind == TabletopSessionKind.GameTemplate
                && request.Selection.GameTemplateId.IsEmpty)
            {
                Add(issues, "GameTemplateIdEmpty", "A Game Template session requires a Template ID.");
            }

            if (request.ActivePlayerIds.Count < 1 || request.ActivePlayerIds.Count > 8)
            {
                Add(issues, "ActivePlayerCountInvalid", "Session Entry supports between one and eight active Players.");
            }

            HashSet<PlayerId> seenPlayers = new HashSet<PlayerId>();
            bool containsRequestingPlayer = false;
            for (int i = 0; i < request.ActivePlayerIds.Count; i++)
            {
                PlayerId playerId = request.ActivePlayerIds[i];
                if (playerId.IsEmpty)
                {
                    Add(issues, "ActivePlayerIdEmpty", "Active Player IDs cannot be empty.");
                }
                else if (!seenPlayers.Add(playerId))
                {
                    Add(issues, "ActivePlayerIdDuplicate", "Active Player IDs must be unique.");
                }

                if (playerId == request.RequestingPlayerId)
                {
                    containsRequestingPlayer = true;
                }
            }

            if (!request.RequestingPlayerId.IsEmpty && !containsRequestingPlayer)
            {
                Add(issues, "RequestingPlayerNotActive", "The initiating Player must be present in the active Player list.");
            }

            return issues;
        }

        private static TabletopSessionBootstrapResult ConstructionFailure(Exception exception)
        {
            return TabletopSessionBootstrapResult.Failure(
                new[]
                {
                    new TabletopSessionBootstrapIssue(
                        "SessionConstructionFailed",
                        $"Session construction failed without exposing a partial Match: {exception.Message}"),
                });
        }

        private static void Add(
            ICollection<TabletopSessionBootstrapIssue> issues,
            string code,
            string message)
        {
            issues.Add(new TabletopSessionBootstrapIssue(code, message));
        }
    }
}
