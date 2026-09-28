using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class Driver : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 6.5f;
    [SerializeField] private float reverseSpeed = 2.7f;
    [SerializeField] private float steerSpeed = 160f;
    private Rigidbody2D body;
    private float throttle;
    private float steering;
    private float touchThrottle;
    private float touchSteer;
    private bool joystickReverse, touchHold;
    private bool touchGas, touchBrake, gasPressed, brakePressed;
    private bool contactLatched;
    private float clearContactSeconds;
    private float currentSpeed;
    private float speedMultiplier = 1f;
    private Coroutine boostCoroutine;
    private readonly System.Collections.Generic.List<RaycastHit2D> hits = new System.Collections.Generic.List<RaycastHit2D>(64);
    private readonly System.Collections.Generic.List<Collider2D> overlaps = new System.Collections.Generic.List<Collider2D>(16);
    public float CurrentSpeed => Mathf.Abs(currentSpeed);
    // Tapak tabrakan = kapsul seukuran badan truk (lebar 1,44, panjang 2,68; gambar 1,49 × 2,76), disusun dari tiga
    // lingkaran di sumbu truk. Dulu satu lingkaran r 1,3: di kiri-kanan 0,55 lebih lebar dari badan, jadi truk
    // "menabrak" padahal masih di aspal hitam (laporan pengguna 26 Sep).
    public const float CollisionRadius = 0.72f;
    public const float FootprintOffset = 0.62f;
    // Lingkaran pemicu (bintang, kilat, dll.) tetap r 1,3 supaya jangkauan ambil tidak berubah; tidak dipakai untuk dinding.
    public const float SensorRadius = 1.3f;
    private static readonly float[] FootprintCenters = { 0f, FootprintOffset, -FootprintOffset };
    // Jarak terjauh tapak dari pusat truk ke arah dunia tertentu (truk menghadap `angle`).
    public static float FootprintReach(float angle, Vector2 worldDirection)
    {
        Vector2 axis = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
        return Mathf.Abs(Vector2.Dot(axis, worldDirection.normalized)) * FootprintOffset + CollisionRadius;
    }
    public static Vector2 FootprintCenter(Vector2 position, float angle, int index)
    {
        return position + (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector2.up) * FootprintCenters[index];
    }
    public const int FootprintCircles = 3;
    public bool IsBoosted => speedMultiplier > 1f;
    public int ImpactCount { get; private set; }
    private float nextImpactTime;
    // Guncangan gambar truk sesudah benturan (hanya visual, tidak memengaruhi tabrakan).
    private const float BumpSeconds = 0.32f;
    private Transform artworkTransform;
    private Vector3 artworkScale = Vector3.one;
    private float bumpStart = -1f, bumpStrength;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.None;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.useFullKinematicContacts = true;
        // Dinding ditangani sapuan kapsul di ResolveMovement (lihat FootprintCenters); collider ini hanya pemicu
        // pickup, bulat supaya jangkauan ambil sama ke segala arah.
        foreach (Collider2D existing in GetComponents<Collider2D>()) existing.enabled = false;
        CircleCollider2D sensor = gameObject.AddComponent<CircleCollider2D>();
        sensor.radius = SensorRadius;
        // Keep the upward-facing source artwork aligned with the controller's +Y.
        Transform artwork = transform.Find("car_0");
        if (artwork != null)
        {
            SpriteRenderer sprite = artwork.GetComponent<SpriteRenderer>();
            artwork.localRotation = Quaternion.identity;
            if (sprite != null && sprite.sprite != null)
            {
                float scale = 2.8f / sprite.sprite.bounds.size.y;
                artwork.localScale = new Vector3(scale * 1.15f, scale, scale);
                artwork.localPosition = -Vector3.Scale(sprite.sprite.bounds.center, artwork.localScale);
            }
            artworkTransform = artwork;
            artworkScale = artwork.localScale;
        }
        moveSpeed = 6.5f;
        reverseSpeed = 2.7f;
        steerSpeed = 160f;
    }

    private void Update()
    {
        if(DeliveryGameManager.Instance!=null && !DeliveryGameManager.IsValidationRunning &&
            (DeliveryGameManager.Instance.MenuOpen || DeliveryGameManager.Instance.Minimap?.Expanded==true))
        {StopImmediately();return;}
        throttle = touchThrottle;
        steering = touchSteer;
        gasPressed=touchGas;brakePressed=touchBrake;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        bool keyboardGas = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
        if (keyboardGas) throttle = 1f;
        gasPressed|=keyboardGas;
        brakePressed|=keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) steering = 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) steering = -1f;
    }

    private void FixedUpdate()
    {
        // Joystick analog: batas laju maju mengikuti jauhnya geseran (tombol GAS/keyboard = 1 = laju penuh).
        float forwardLimit = moveSpeed * speedMultiplier * Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(throttle));
        currentSpeed = touchHold && !gasPressed && !brakePressed
            ? Mathf.MoveTowards(currentSpeed, 0f, 14f * Time.fixedDeltaTime)
            : AdvanceSpeed(currentSpeed,gasPressed,brakePressed,forwardLimit,reverseSpeed,Time.fixedDeltaTime);
        float turn = steering * steerSpeed * Time.fixedDeltaTime;
        // Steering keeps the same left/right meaning when stopped or reversing.
        float angle = body.rotation + turn;
        Vector2 start = body.position;
        // Tapak lonjong: berbelok di samping dinding bisa memasukkan ujung badan ke dinding. Dorong keluar sedikit
        // (terasa seperti ban menyentuh trotoar); kalau tidak bisa (celah sempit), belokan frame ini dibatalkan.
        if (!Mathf.Approximately(turn, 0f) && !Depenetrate(ref start, angle)) { angle = body.rotation; start = body.position; }
        Vector2 forward = Quaternion.Euler(0, 0, angle) * Vector2.up;
        Vector2 displacement = forward * currentSpeed * Time.fixedDeltaTime;
        Vector2 position = ResolveMovement(start, displacement, angle);
        float blocked=Vector2.Distance(position,start+displacement);
        bool contact=blocked>0.008f;
        // A glancing scrape or slow parking contact must not damage fragile deliveries.
        if (!contactLatched && IsMeaningfulImpact(currentSpeed, blocked, displacement.magnitude))
        {
            // A tiny first touch must not suppress the following real impact.
            contactLatched=true;
            if (Time.time>=nextImpactTime)
            {
                ImpactCount++;
                DeliveryAudio.Play(DeliveryAudio.Cue.Bump);
                nextImpactTime = Time.time + 0.8f;
                bumpStart = Time.time;
                bumpStrength = Mathf.InverseLerp(2.2f, 6.5f, Mathf.Abs(currentSpeed));
            }
        }
        if(contact)clearContactSeconds=0;
        else
        {
            clearContactSeconds+=Time.fixedDeltaTime;
            if(clearContactSeconds>=0.15f)contactLatched=false;
        }
        // Laju mengikuti gerak yang benar-benar terjadi: menabrak lurus berhenti, menyerempet atau menggeser sepanjang
        // dinding tetap melaju sesuai komponen yang lolos (dulu: >80% tertahan langsung 0, <80% tetap laju penuh,
        // sehingga tabrakan terasa patah-patah).
        if(contact)
        {
            float achieved=Vector2.Distance(position,start)/Mathf.Max(Time.fixedDeltaTime,0.0001f);
            currentSpeed=Mathf.Sign(currentSpeed)*Mathf.Min(Mathf.Abs(currentSpeed),achieved);
        }
        body.MoveRotation(angle);
        body.MovePosition(position);
    }

    // Guncangan singkat gambar truk saat benturan terhitung: miring kiri-kanan dan sedikit gepeng, meluruh 0,32 dtk.
    private void LateUpdate()
    {
        if (artworkTransform == null || bumpStart < 0f) return;
        float t = (Time.time - bumpStart) / BumpSeconds;
        if (t >= 1f || t < 0f)
        {
            bumpStart = -1f;
            artworkTransform.localRotation = Quaternion.identity;
            artworkTransform.localScale = artworkScale;
            return;
        }
        float decay = (1f - t) * (1f - t);
        float strength = Mathf.Lerp(0.5f, 1f, bumpStrength);
        artworkTransform.localRotation = Quaternion.Euler(0f, 0f, 6f * strength * decay * Mathf.Sin(t * Mathf.PI * 3f));
        float squash = 0.07f * strength * decay;
        artworkTransform.localScale = Vector3.Scale(artworkScale, new Vector3(1f + squash, 1f - squash, 1f));
    }

    public static bool IsMeaningfulImpact(float speed, float blockedDistance, float requestedDistance)
    {
        return Mathf.Abs(speed)>=2.2f && blockedDistance>0.008f
            && requestedDistance>0 && blockedDistance>requestedDistance*0.45f;
    }

    // Menghadap sesuai rotasi truk saat ini (dipakai tes yang hanya memberi posisi).
    public Vector2 ResolveMovement(Vector2 position, Vector2 displacement)
    {
        return ResolveMovement(position, displacement, body != null ? body.rotation : transform.eulerAngles.z);
    }

    public Vector2 ResolveMovement(Vector2 position, Vector2 displacement, float angle)
    {
        // Limit each sweep at tight concave junctions, including unusually large frame steps.
        int steps = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / 0.25f));
        Vector2 step = displacement / steps;
        for (int i = 0; i < steps; i++) position = SweepStep(position, step, angle);
        return position;
    }

    private static ContactFilter2D BarrierFilter()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(1 << DeliveryTown.BarrierLayer);
        filter.useTriggers = false;
        return filter;
    }

    // Dorong tapak keluar dari dinding yang dimasukinya (sesudah berputar). Maksimal 0,3 unit per frame; gagal kalau
    // masih menempel sesudah beberapa langkah (misalnya terjepit di antara dua dinding), dan posisi tidak diubah.
    public bool Depenetrate(ref Vector2 position, float angle)
    {
        ContactFilter2D filter = BarrierFilter();
        Vector2 moved = position;
        for (int iteration = 0; iteration < 6; iteration++)
        {
            Vector2 push = Vector2.zero;
            float deepest = 0.002f;
            for (int c = 0; c < FootprintCircles; c++)
            {
                Vector2 center = FootprintCenter(moved, angle, c);
                int count = Physics2D.OverlapCircle(center, CollisionRadius, filter, overlaps);
                for (int i = 0; i < count; i++)
                {
                    Vector2 outward = center - overlaps[i].ClosestPoint(center);
                    float gap = outward.magnitude;
                    if (gap < 0.0001f) return false;
                    float depth = CollisionRadius - gap;
                    if (depth > deepest) { deepest = depth; push = outward / gap * (depth + 0.01f); }
                }
            }
            if (push == Vector2.zero)
            {
                position = moved;
                return true;
            }
            moved += push;
            if ((moved - position).sqrMagnitude > 0.09f) return false;
        }
        return false;
    }

    private Vector2 SweepStep(Vector2 position, Vector2 displacement, float angle)
    {
        ContactFilter2D filter = BarrierFilter();
        // Sweep and slide twice; swept collision prevents crossing a curb at high speed.
        for (int pass = 0; pass < 2 && displacement.sqrMagnitude > 0.000001f; pass++)
        {
            float distance = displacement.magnitude;
            RaycastHit2D nearest = default;
            float nearestDistance = distance + 1f;
            // Tiga lingkaran di sumbu truk = kapsul badan; celah di sisi antar-lingkaran < 0,08.
            for (int c = 0; c < FootprintCircles; c++)
            {
                Vector2 center = FootprintCenter(position, angle, c);
                int count = Physics2D.CircleCast(center, CollisionRadius, displacement / distance,
                    filter, hits, distance + 0.02f);
                for (int i = 0; i < count; i++)
                {
                    RaycastHit2D hit = hits[i];
                    if (hit.distance <= 0.001f)
                    {
                        // Start-overlap casts return -castDirection, not the surface normal.
                        // Recover the actual outward normal so reversing never hits the same wall again.
                        Vector2 outward = center - hit.collider.ClosestPoint(center);
                        if (outward.sqrMagnitude > 0.000001f)
                        {
                            hit.normal = outward.normalized;
                            if (Vector2.Dot(displacement, hit.normal) >= -0.000001f) continue;
                        }
                    }
                    if (hit.distance < nearestDistance)
                    {
                        nearest = hit;
                        nearestDistance = hit.distance;
                    }
                }
            }
            if (nearest.collider == null)
            {
                position += displacement;
                break;
            }
            float travel = Mathf.Clamp(nearestDistance - 0.02f, 0f, distance);
            position += displacement.normalized * travel;
            Vector2 remainder = displacement.normalized * Mathf.Max(0f, distance - travel);
            displacement = remainder - Mathf.Min(0f, Vector2.Dot(remainder, nearest.normal)) * nearest.normal;
        }
        return position;
    }

    public void SetTouchInput(float forward, float turn)
    {
        touchHold = false;
        touchThrottle = Mathf.Clamp(forward, -1f, 1f);
        touchGas=forward>0;touchBrake=forward<0;
        touchSteer = Mathf.Clamp(turn, -1f, 1f);
    }

    public void SetTouchControls(bool gas,bool brake,float turn)
    {touchHold=false;touchGas=gas;touchBrake=brake;touchThrottle=gas?1f:0f;touchSteer=Mathf.Clamp(turn,-1,1);}

    // Joystick portrait: arah jempol = arah di layar. Ke depan/samping truk = maju sambil berbelok ke arah itu.
    // Uji HP 27 Sep ("susah mundur"): ke belakang truk (> 125°, kembali maju < 100°) kini mundur sungguhan dan buntut
    // truk berbelok ke arah jempol; dulu ke belakang = putar balik pelan, jadi truk tidak bisa mundur dari tembok/zona.
    // Dipanggil tiap frame selama jempol menahan, supaya belokan berhenti begitu truk menghadap arah jempol.
    public void SetDirectionalInput(Vector2 worldDirection, float analogThrottle)
    {
        if (worldDirection.sqrMagnitude < 0.0001f || analogThrottle <= 0f)
        {
            HoldStill();
            return;
        }
        Vector2 direction = worldDirection.normalized;
        float angle = Vector2.SignedAngle(transform.up, direction);
        if (Mathf.Abs(angle) > 125f) joystickReverse = true;
        else if (Mathf.Abs(angle) < 100f) joystickReverse = false;
        float power = Mathf.Clamp01(analogThrottle);
        if (joystickReverse)
        {
            float rearAngle = Vector2.SignedAngle(-(Vector2)transform.up, direction);
            SetTouchInput(-power, Mathf.Clamp(rearAngle / 35f, -1f, 1f));
            return;
        }
        SetTouchInput(power, Mathf.Clamp(angle / 35f, -1f, 1f));
    }

    // Jempol dilepas (uji HP 27 Sep: "susah berhenti di area"): truk direm sampai diam (± 0,5 dtk dari laju penuh),
    // tidak meluncur 1,3 dtk seperti melepas gas, dan tidak berlanjut mundur.
    public void HoldStill()
    {
        SetTouchInput(0f, 0f);
        touchHold = true;
        joystickReverse = false;
    }

    public static float AdvanceSpeed(float speed,bool gas,bool brake,float forwardLimit,float reverseLimit,float seconds)
    {
        seconds=Mathf.Max(0,seconds);
        if(brake && (gas || speed>0.01f))return Mathf.MoveTowards(speed,0,14f*seconds);
        if(brake)return Mathf.MoveTowards(speed,-reverseLimit,4.5f*seconds);
        if(gas)return Mathf.MoveTowards(speed,forwardLimit,(speed<0?14f:7f)*seconds);
        return Mathf.MoveTowards(speed,0,5f*seconds);
    }

    public void PlaceAt(Vector2 position, float angle = 0f)
    {
        StopImmediately();
        contactLatched=false;clearContactSeconds=0;
        body.position = position;
        body.rotation = angle;
        transform.SetPositionAndRotation(new Vector3(position.x, position.y, 0f), Quaternion.Euler(0, 0, angle));
    }

    public void StopImmediately()
    {
        throttle = 0f;
        steering = 0f;
        touchThrottle = 0f;
        touchSteer = 0f;
        currentSpeed = 0f;
        touchGas=false;touchBrake=false;gasPressed=false;brakePressed=false;
        if (body == null) return;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) StopImmediately();
    }

    public void StartBoost(float duration)
    {
        if (boostCoroutine != null) StopCoroutine(boostCoroutine);
        boostCoroutine = StartCoroutine(Boost(duration));
    }

    public void ClearBoost()
    {
        if (boostCoroutine != null) StopCoroutine(boostCoroutine);
        boostCoroutine = null;
        speedMultiplier = 1f;
    }

    public void SetLivery(int index)
    {
        var artwork=transform.Find("car_0");
        if(artwork==null)return;
        var sprite=artwork.GetComponent<SpriteRenderer>();
        index=Mathf.Clamp(index,0,CourierProgress.Names.Length-1);
        var skins=Resources.Load<TruckSkinCatalog>("TruckSkinCatalog");
        if(sprite!=null)
        {
            if(skins!=null && skins.Get(index)!=null)sprite.sprite=skins.Get(index);
            sprite.color=Color.white;
            float heightScale=2.8f/sprite.sprite.bounds.size.y;
            artwork.localScale=new Vector3(heightScale*1.15f,heightScale,heightScale);
            artwork.localPosition=-Vector3.Scale(sprite.sprite.bounds.center,artwork.localScale);
            artworkTransform=artwork;artworkScale=artwork.localScale;
        }
    }

    public void SetLiverySprite(Sprite livery)
    {
        if (livery == null) return;
        Transform artwork = transform.Find("car_0");
        SpriteRenderer sprite = artwork == null ? null : artwork.GetComponent<SpriteRenderer>();
        if (sprite == null) return;
        sprite.sprite = livery;
        sprite.color = Color.white;
        float heightScale = 2.8f / sprite.sprite.bounds.size.y;
        artwork.localScale = new Vector3(heightScale * 1.15f, heightScale, heightScale);
        artwork.localPosition = -Vector3.Scale(sprite.sprite.bounds.center, artwork.localScale);
        artworkTransform = artwork;
        artworkScale = artwork.localScale;
    }

    private IEnumerator Boost(float duration)
    {
        speedMultiplier = 1.2f;
        yield return new WaitForSeconds(duration);
        speedMultiplier = 1f;
        boostCoroutine = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        DeliveryGameManager.Instance?.TryInteract(other.gameObject);
    }
}
