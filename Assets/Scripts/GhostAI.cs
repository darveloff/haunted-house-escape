using UnityEngine;
using UnityEngine.AI;

public enum GhostKind { Wisp, Wraith, Watcher, Screamer }
public enum GhostState { Patrol, Chase, Search }

[System.Serializable]
public struct GhostProfile
{
    public string name;
    public string tagline;
    public string[] traits;
    public Color color;
    public float patrolSpeed, chaseSpeed;
    public float sightRange, viewAngle, hearingRange;
    public float moanPitch;
}

// Patrols waypoints. Sees the player only inside a view cone with clear line of sight,
// hears sprinting nearby, chases to where it last saw you, searches, then gives up.
// Each GhostKind has its own stats (see Profiles).
[RequireComponent(typeof(NavMeshAgent))]
public class GhostAI : MonoBehaviour
{
    // Tune ghost types here.
    public static readonly GhostProfile[] Profiles =
    {
        new GhostProfile { name = "WISP", tagline = "Average at everything. Learn its route and slip past.",
            traits = new[] { "STEADY", "PREDICTABLE" },
            color = new Color(0.35f, 0.65f, 1f), patrolSpeed = 2f, chaseSpeed = 4f, sightRange = 9f, viewAngle = 120f, hearingRange = 7f, moanPitch = 1f },
        new GhostProfile { name = "WRAITH", tagline = "Blazing fast but nearly blind. It hunts by sound.",
            traits = new[] { "VERY FAST", "HALF-BLIND", "HEARS SPRINTS" },
            color = new Color(1f, 0.28f, 0.22f), patrolSpeed = 2.8f, chaseSpeed = 6f, sightRange = 3.5f, viewAngle = 70f, hearingRange = 12f, moanPitch = 1.3f },
        new GhostProfile { name = "WATCHER", tagline = "Slow and patient, but sees across whole rooms.",
            traits = new[] { "SLOW", "SEES FAR", "WIDE VIEW" },
            color = new Color(0.72f, 0.4f, 1f), patrolSpeed = 1.4f, chaseSpeed = 3.2f, sightRange = 16f, viewAngle = 170f, hearingRange = 5f, moanPitch = 0.75f },
        new GhostProfile { name = "SCREAMER", tagline = "Shrieks when it sees you, calling ghosts nearby.",
            traits = new[] { "SHRIEKS", "CALLS FRIENDS" },
            color = new Color(0.4f, 1f, 0.5f), patrolSpeed = 1.8f, chaseSpeed = 3.6f, sightRange = 8f, viewAngle = 120f, hearingRange = 7f, moanPitch = 1.1f },
    };

    public GhostKind kind;
    public Transform[] waypoints;
    public float touchRange = 1.6f;
    public float searchTime = 3f;
    public float catchDistance = 1f;
    public float screamAlertRadius = 25f;

    public Transform visual;
    public Animator animator;
    public Light glow;
    public float bobHeight = 0.25f;

    NavMeshAgent agent;
    PlayerController player;
    AudioSource voice;
    GhostState state = GhostState.Patrol;
    float screamUntil;
    int waypointIndex;
    Vector3 lastKnownPosition;
    float searchUntil;
    float nextRepath;
    float nextMoan;
    Vector3 visualBase;
    float bobOffset;
    float glowBase;

    public GhostProfile Profile => Profiles[(int)kind];
    public bool IsChasing => state == GhostState.Chase;
    public GhostState CurrentState => state;
    public bool IsScreaming => Time.time < screamUntil;
    public NavMeshAgent Agent => agent;

    public event System.Action<GhostState> StateChanged;
    public event System.Action Screamed;

    // 0..1 for the HUD danger meter and heartbeat.
    public float Alertness
    {
        get
        {
            if (state == GhostState.Chase) return 1f;
            if (state == GhostState.Search) return 0.6f;
            if (player == null) return 0f;
            return Mathf.Clamp01(1f - Vector3.Distance(transform.position, player.transform.position) / 15f) * 0.4f;
        }
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        player = FindAnyObjectByType<PlayerController>();
        if (visual != null) visualBase = visual.localPosition;
        bobOffset = Random.value * 10f;
        agent.speed = Profile.patrolSpeed;
        if (glow != null)
        {
            glow.color = Profile.color;
            glowBase = glow.intensity;
        }

        voice = gameObject.AddComponent<AudioSource>();
        voice.spatialBlend = 1f;
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.minDistance = 2f;
        voice.maxDistance = 22f;
        voice.dopplerLevel = 0f;
        voice.playOnAwake = false;
        nextMoan = Time.time + Random.Range(1f, 6f);

        GoToNextWaypoint();
    }

    void Update()
    {
        if (visual != null)
            visual.localPosition = visualBase + Vector3.up * (Mathf.Sin(Time.time * 2f + bobOffset) + 1f) * bobHeight;

        var gm = GameManager.Instance;
        if (gm != null && gm.IsOver)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            if (animator != null) animator.SetFloat("Speed", 0f);
            return;
        }
        if (gm != null && gm.CurrentState != GameManager.State.Playing) return;
        if (player == null || !agent.isOnNavMesh) return;

        if (CanDetectPlayer()) Alert(player.transform.position, spottedDirectly: true);

        var p = Profile;
        switch (state)
        {
            case GhostState.Chase:
                agent.isStopped = IsScreaming;
                agent.updateRotation = true;
                agent.speed = p.chaseSpeed;
                if (Time.time >= nextRepath)
                {
                    agent.SetDestination(lastKnownPosition);
                    nextRepath = Time.time + 0.2f;
                }
                // Reached the spot where we lost them: look around.
                if (!agent.pathPending && agent.remainingDistance < 0.8f &&
                    Vector3.Distance(player.transform.position, lastKnownPosition) > 0.5f)
                {
                    SetState(GhostState.Search);
                    searchUntil = Time.time + searchTime;
                }
                break;

            case GhostState.Search:
                agent.isStopped = true;
                agent.updateRotation = false;
                transform.Rotate(0f, 140f * Time.deltaTime, 0f);
                if (Time.time > searchUntil)
                {
                    SetState(GhostState.Patrol);
                    agent.isStopped = false;
                    agent.updateRotation = true;
                    GoToNextWaypoint();
                }
                break;

            case GhostState.Patrol:
                agent.speed = p.patrolSpeed;
                if (!agent.pathPending && agent.remainingDistance < 0.6f)
                    GoToNextWaypoint();
                break;
        }

        UpdateGlow();
        UpdateVoice();
        if (animator != null)
            animator.SetFloat("Speed", Mathf.Min(2f, agent.velocity.magnitude / 2f), 0.1f, Time.deltaTime);

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        if (!player.IsHidden && toPlayer.magnitude < catchDistance)
        {
            player.Die();
            gm?.LoseCaught(kind);
        }
    }

    // Start (or keep) chasing towards a position. Screamers pass it on to nearby ghosts.
    public void Alert(Vector3 position, bool spottedDirectly)
    {
        bool wasChasing = state == GhostState.Chase;
        lastKnownPosition = position;
        if (wasChasing) return;
        SetState(GhostState.Chase);

        if (spottedDirectly)
        {
            AudioManager.Instance?.Spotted();
            GameManager.Instance?.CountSpotted();
        }
        nextMoan = Time.time + 0.3f;

        if (kind == GhostKind.Screamer && spottedDirectly)
        {
            voice.pitch = 1f;
            voice.PlayOneShot(AudioSynth.Shriek(), 0.45f);
            screamUntil = Time.time + 1.3f; // stands still while it shrieks
            Screamed?.Invoke();
            GameManager.Instance?.ShowMessage("THE SCREAMER SHRIEKED!\nOTHER GHOSTS ARE COMING", 3f);
            foreach (var other in FindObjectsByType<GhostAI>(FindObjectsSortMode.None))
                if (other != this && Vector3.Distance(other.transform.position, position) < screamAlertRadius)
                    other.Alert(position, spottedDirectly: false);
        }
    }

    void SetState(GhostState next)
    {
        if (state == next) return;
        state = next;
        StateChanged?.Invoke(next);
    }

    void UpdateGlow()
    {
        if (glow == null) return;
        var c = Profile.color;
        Color target = state == GhostState.Chase ? Color.Lerp(c, Color.white, 0.25f)
                     : state == GhostState.Search ? Color.Lerp(c, new Color(1f, 0.7f, 0.2f), 0.5f) : c;
        glow.color = Color.Lerp(glow.color, target, Time.deltaTime * 4f);
        float pulse = state == GhostState.Chase ? 1.6f + 0.4f * Mathf.Sin(Time.time * 12f) : 1f;
        glow.intensity = glowBase * pulse;
    }

    void UpdateVoice()
    {
        if (Time.time < nextMoan) return;
        bool chasing = state == GhostState.Chase;
        voice.pitch = Profile.moanPitch * (chasing ? 1.15f : 1f) * Random.Range(0.94f, 1.06f);
        voice.PlayOneShot(AudioSynth.Moan(Random.Range(0, 2)), chasing ? 0.4f : 0.22f);
        nextMoan = Time.time + (chasing ? Random.Range(3.5f, 5f) : Random.Range(9f, 16f));
    }

    bool CanDetectPlayer()
    {
        if (player.IsHidden) return false;
        var p = Profile;

        Vector3 eye = transform.position + Vector3.up;
        Vector3 target = player.transform.position + Vector3.up;
        Vector3 toTarget = target - eye;
        float dist = toTarget.magnitude;

        if (dist < touchRange) return true;
        if (player.IsSprinting && dist < p.hearingRange) return true;

        if (dist > p.sightRange) return false;
        Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
        if (Vector3.Angle(transform.forward, flat) > p.viewAngle * 0.5f) return false;

        if (Physics.Raycast(eye, toTarget.normalized, out var hit, dist + 0.5f, ~0, QueryTriggerInteraction.Ignore))
            return hit.transform == player.transform || hit.transform.IsChildOf(player.transform);
        return false;
    }

    void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        agent.SetDestination(waypoints[waypointIndex].position);
        waypointIndex = (waypointIndex + 1) % waypoints.Length;
    }
}
