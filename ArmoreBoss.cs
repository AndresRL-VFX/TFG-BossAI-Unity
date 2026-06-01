using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ArmoreBoss : MonoBehaviour 
{ 
 public int maxHealth, currentHealth, poiseHealth;
    public Animator trapToRelease;
    public float speed;
 public Animator iaArmore, animArmore;
 public int guardState;
 public GameObject player, Holder_1, Holder_2,musicRadius;
    public BA_AnimationEvent armoreAnimScript;   

 public GameObject vfxHitEffect;

 public GameObject vfxHitEffectFinish;

 public Slider slider;

 public Transform vfxSpawn;

 private CinemachineImpulseSource cinemachineImpulseSource;

    public bool secondFaceNow;

// Start is called before the first frame update
 void Start()
 {
        secondFaceNow = false;
    guardState = 1;
    player = GameObject.FindGameObjectWithTag("Player");
    cinemachineImpulseSource = this.GetComponent<CinemachineImpulseSource>();
 }

// Update is called once per frame
 void Update()
 {
    slider.value = currentHealth;
 }
 public void TakeDamage(int damage)
 {
    if (guardState == 1)
    {

        //Debug.Log("recibiria daño");
        cinemachineImpulseSource.GenerateImpulse();
        MiFmod.Instance.Play("SFX_2d/RecibirDañoBoss");
        StartCoroutine(FrameFreeze(0.05f));
            
        Instantiate(vfxHitEffect, vfxSpawn.transform.position, Quaternion.identity);
        currentHealth -= damage;

            if (currentHealth <= maxHealth/2)
            {
                SecodFace();
            }
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            MiFmod.Instance.StopFondo();
            animArmore.SetTrigger("Death");
            Instantiate(vfxHitEffectFinish, vfxSpawn.transform.position, Quaternion.identity);
            iaArmore.SetTrigger("Death");
            
        }
    }
    else if (guardState == 2)
    {
        //is bloquing

        animArmore.SetTrigger("ShieldCounter");
        //animArmore.SetTrigger("Block");
        guardState = 1;
    }
    else if (guardState == 3)
    {
        //is healing
        poiseHealth -= damage;
        cinemachineImpulseSource.GenerateImpulse();
        StartCoroutine(FrameFreeze(0.05f));

        Instantiate(vfxHitEffect, vfxSpawn.transform.position, Quaternion.identity);
        if (poiseHealth <= 0)
        {
            Instantiate(vfxHitEffectFinish, vfxSpawn.transform.position, Quaternion.identity);
            poiseHealth = 0;
            animArmore.SetTrigger("ShieldRelease");
            guardState = 1;

        }
        //animArmore.SetTrigger("Healing");

    }

}

 private IEnumerator FrameFreeze(float duration)
 {
    Time.timeScale = 0f;
    yield return new WaitForSecondsRealtime(duration);

    Time.timeScale = 1f;
 }
    public void SecodFace()
    {
        armoreAnimScript = animArmore.gameObject.GetComponent<BA_AnimationEvent>();
        //armoreAnimScript.holderProyectile1 = Holder_2;
        secondFaceNow = true;
        Holder_1.SetActive(true);
    }
    public void EndFace()
    {
        Holder_1.SetActive(false);
        Destroy(musicRadius);
        MiFmod.Instance.StopFondo();
    }
 //Metodos de cambio de estado necesarios
 public void AnimArmoreWalk(int value)
 {
    animArmore.SetFloat("Speed", value);
 }
 public void AnimArmoreSmash()
 {
    animArmore.SetTrigger("Smash");
 }
 public void AnimArmoreSwipe()
 {
    animArmore.SetTrigger("Swipe");

 }
 public void AnimArmoreBlock()
 {
    animArmore.SetTrigger("Shield");


 }
 public void ArmoreHitme()
 {
    iaArmore.SetTrigger("HitMe");
 }
 public void AnimArmoreShieldRelease()
 {
    animArmore.SetTrigger("ShieldRelease");
 }
 public void ArmoreIdle()
 {
    animArmore.SetTrigger("Idle");
 }
 public void AnimArmoreShieldCounter()
 {
    animArmore.SetTrigger("ShieldCounter");
 }
 public void AnimArmoreShoot()
 {
    animArmore.SetTrigger("Shoot");
 }
 public void ArmoreAction()
 {
    iaArmore.SetTrigger("Action");
 }
 public void ArmoreActivate()
 {
    animArmore.SetTrigger("Activate");
 }
}
