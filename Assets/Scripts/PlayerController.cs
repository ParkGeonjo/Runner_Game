using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Jump")]
    public AudioClip deathClip;                       // (충돌) 효과음
    [SerializeField] private AudioClip gameOverClip;  // 사망 사운드(즉시 재생)
    public float jumpForce = 700f;
    public int maxJumpCount = 2;

    private int jumpCount = 0;
    public bool IsGrounded { get; private set; }
    private bool isDead = false;

    private Rigidbody2D playerRigidbody;
    private Animator animator;
    private AudioSource playerAudio;

    [Header("FX")]
    [SerializeField] private ParticleSystem runLoopFx;
    [SerializeField] private ParticleSystem jumpFxPrefab;
    [SerializeField] private Vector3 jumpFxOffset = new Vector3(0f, -1f, 0f);

    private readonly HashSet<Collider2D> groundContacts = new();
    [SerializeField] private float groundedGrace = 0.12f;
    private float lastGroundedTime = -999f;

    [Header("Slide")]
    [SerializeField] private AudioClip slideClip;
    private BoxCollider2D boxCol;
    private Vector2 boxSizeDefault;
    private Vector2 boxOffsetDefault;
    private bool isSliding = false;
    private bool slideHeld = false;

    public Collider2D MainCollider => boxCol;

    [Header("Animator Defaults")]
    [SerializeField] private string reviveResumeState = "Run"; // ← 기본 복귀 상태명(애니메이터 기준)
    [SerializeField] private int reviveResumeLayer = 0;        // ← 보통 Base Layer = 0

    private int _reviveResumeHash;

    private void Start()
    {
        playerRigidbody = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        playerAudio = GetComponent<AudioSource>();

        playerRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        playerRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;

        boxCol = GetComponent<BoxCollider2D>();
        if (boxCol != null) { boxSizeDefault = boxCol.size; boxOffsetDefault = boxCol.offset; }

        _reviveResumeHash = !string.IsNullOrEmpty(reviveResumeState)
        ? Animator.StringToHash(reviveResumeState)
        : 0;

        // InGameManager에 자기 자신 등록(씬 전환/리로드 등 안전망)
        if (InGameManager.instance != null)
            InGameManager.instance.RegisterPlayer(this);

        SetRunLoop(false);
    }

    private void Update()
    {
        if (isDead) return;

        bool groundedNow = groundContacts.Count > 0 || (Time.time - lastGroundedTime) <= groundedGrace;
        IsGrounded = groundedNow;
        animator?.SetBool("Grounded", IsGrounded);
        SetRunLoop(IsGrounded);

        if (InGameManager.instance != null && InGameManager.instance.IsReviving)
        {
            if (isSliding) StopSlide();
            return;
        }

        if (slideHeld && !isSliding && CanSlideStart()) StartSlide();
        if (isSliding && (!slideHeld || !IsOnGroundStrict())) StopSlide();
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (isDead) return;
        if (InGameManager.instance != null && InGameManager.instance.IsReviving) return;

        if (ctx.performed)
        {
            SetRunLoop(false);
            SpawnJumpFx();
            TryJump();
        }
        else if (ctx.canceled && playerRigidbody.linearVelocity.y > 0)
        {
            playerRigidbody.linearVelocity = new Vector2(
                playerRigidbody.linearVelocity.x,
                playerRigidbody.linearVelocity.y * 0.5f);
        }
    }

    public void OnSlide(InputAction.CallbackContext ctx)
    {
        if (isDead) return;
        if (InGameManager.instance != null && InGameManager.instance.IsReviving) return;

        if (ctx.performed)
        {
            slideHeld = true;
            if (!isSliding && CanSlideStart()) StartSlide();
        }
        else if (ctx.canceled)
        {
            slideHeld = false;
            if (isSliding) StopSlide();
        }
    }

    private bool CanSlideStart() => IsOnGroundStrict();
    private bool IsOnGroundStrict() => groundContacts.Count > 0;
    private bool IsOnGroundGrace() => groundContacts.Count > 0 || (Time.time - lastGroundedTime) <= groundedGrace;

    private void TryJump()
    {
        if (jumpCount >= maxJumpCount) return;

        jumpCount++;
        playerRigidbody.linearVelocity = Vector2.zero;
        playerRigidbody.AddForce(new Vector2(0, jumpForce));
        playerAudio.volume = BgmManager.Instance ? BgmManager.Instance.EffectiveSfxVolume : 1f;
        playerAudio?.Play();
        IsGrounded = false;
        animator?.SetBool("Grounded", false);
        if (jumpCount == 2) animator?.SetTrigger("DoubleJump");
    }

    private void StartSlide()
    {
        // 💡 혹시라도 호출 타이밍이 애매할 때를 대비해, 진짜 접지인지 재확인
        if (!IsOnGroundStrict()) return;

        isSliding = true;
        animator?.SetBool("Slide", true);

        if (boxCol != null)
        {
            boxCol.size = new Vector2(1.3f, 0.6f);
            boxCol.offset = new Vector2(boxCol.offset.x, -1.5f);
        }

        if (slideClip)
        {
            playerAudio.volume = BgmManager.Instance ? BgmManager.Instance.EffectiveSfxVolume : 1f;
            playerAudio.PlayOneShot(slideClip); // ⬅ 슬라이드 시작마다 1회 정확히 재생
        }
    }
    private void StopSlide()
    {
        isSliding = false;
        animator?.SetBool("Slide", false);
        if (boxCol != null) { boxCol.size = boxSizeDefault; boxCol.offset = boxOffsetDefault; }
    }

    public void PlayTimeDeath()
    {
        if (isDead) return;
        animator?.SetTrigger("Die");
        DoDieCommon();
    }

    public void PlayCrashDeath()
    {
        if (isDead) return;
        animator?.SetTrigger("CrashDie");
        if (deathClip)
        {
            playerAudio.volume = BgmManager.Instance ? BgmManager.Instance.EffectiveSfxVolume : 1f;
            playerAudio.PlayOneShot(deathClip);
        }
        DoDieCommon();
    }

    public void PlayCrash()
    {
        //if (isDead) return;
        animator?.SetTrigger("Crash");
        if (deathClip)
        {
            playerAudio.volume = BgmManager.Instance ? BgmManager.Instance.EffectiveSfxVolume : 1f;
            playerAudio.PlayOneShot(deathClip);
        }
    }

    private void DoDieCommon()
    {
        isDead = true;
        playerRigidbody.linearVelocity = Vector2.zero;

        if (isSliding) StopSlide();
        if (runLoopFx && runLoopFx.isPlaying) runLoopFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        SetRunLoop(false);

        // 사망 즉시 사운드
        if (gameOverClip)
        {
            playerAudio.volume = BgmManager.Instance ? BgmManager.Instance.EffectiveSfxVolume : 1f;
            playerAudio.PlayOneShot(gameOverClip);
        }

        InGameManager.instance.OnPlayerDead();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;

        if (other.CompareTag("Fall"))
        {
            // 낙사: 체력 전량 소진 + 즉시 사망, 부활 금지
            if (InGameManager.instance) InGameManager.instance.FallDeath();
            return;
        }

        if (other.tag == "Dead")
            InGameManager.instance.AddHp(-10, true);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.contacts.Length > 0 && collision.contacts[0].normal.y > 0.7f)
        {
            groundContacts.Add(collision.collider);
            lastGroundedTime = Time.time;
            IsGrounded = true;
            jumpCount = 0;
            animator?.SetBool("Grounded", true);
            SetRunLoop(true);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.contacts.Length > 0 && collision.contacts[0].normal.y > 0.7f)
        {
            groundContacts.Add(collision.collider);
            lastGroundedTime = Time.time;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        groundContacts.Remove(collision.collider);
        if (groundContacts.Count == 0)
        {
            IsGrounded = false;
            animator?.SetBool("Grounded", false);
            SetRunLoop(false);
            if (isSliding) StopSlide();
        }
    }

    private void SetRunLoop(bool on)
    {
        if (!runLoopFx) return;
        var em = runLoopFx.emission;
        em.enabled = on;
        if (on) { if (!runLoopFx.isPlaying) runLoopFx.Play(); }
        else { if (runLoopFx.isPlaying) runLoopFx.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
    }

    private void SpawnJumpFx()
    {
        if (!jumpFxPrefab) return;
        var fx = Instantiate(jumpFxPrefab, transform.position + (Vector3)jumpFxOffset, Quaternion.identity);
        fx.Play();
        var main = fx.main;
        float life = main.duration + main.startLifetime.constantMax;
        Destroy(fx.gameObject, life + 0.1f);
    }

    public SpriteRenderer[] GetSpriteRenderers()
    {
        return GetComponentsInChildren<SpriteRenderer>(true);
    }

    public void RestoreAfterRevive()
    {
        // 사망 플래그/속도/슬라이드 정리
        isDead = false;
        if (isSliding) StopSlide();
        jumpCount = 0;
        playerRigidbody.linearVelocity = Vector2.zero;

        slideHeld = false;

        // 접지 판정 최신화
        bool groundedNow = groundContacts.Count > 0 || (Time.time - lastGroundedTime) <= groundedGrace;
        IsGrounded = groundedNow;

        // 애니메이터 상태/트리거 정리 + 기본 상태로 강제 전이
        if (animator)
        {
            // 트리거 리셋
            animator.ResetTrigger("Die");
            animator.ResetTrigger("CrashDie");
            animator.ResetTrigger("Crash");
            animator.SetBool("Slide", false);
            animator.SetBool("Grounded", IsGrounded);

            // ★ 핵심: 기본 복귀 상태로 0프레임부터 전이
            if (_reviveResumeHash != 0)
            {
                // CrossFade(스무스) 또는 Play(즉시). 딱 끊김 방지 위해 짧게 스무스
                animator.CrossFade(_reviveResumeHash, 0.05f, reviveResumeLayer, 0f);
            }
            else
            {
                // 혹시 상태명이 비어있으면 현재 스테이트를 0으로 리셋
                var st = animator.GetCurrentAnimatorStateInfo(reviveResumeLayer);
                animator.Play(st.fullPathHash, reviveResumeLayer, 0f);
            }
        }

        // 러닝 FX는 접지일 때만
        SetRunLoop(IsGrounded);
    }
}
