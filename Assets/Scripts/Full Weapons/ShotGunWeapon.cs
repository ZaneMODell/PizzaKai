using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.Serialization;
using Update = Unity.VisualScripting.Update;

/// <summary>
/// Represents the Shotgun, child class of WeaponModule
/// In charge of initiating and completing the shotgun dash attack
/// <br/>
/// Authors: Suraj Karthikeyan (2023/2024)
/// </summary>
public class ShotGunWeapon : WeaponModule
{
    #region Variables
    [Header("Alt Fire Settings")]
    [Tooltip("Power with which the player is sent flying")]
    [SerializeField] private float pushPower = 20f;
    [Tooltip("Dash damage of shotgun dash")]
    [SerializeField] public int dashDamage = 3;
    [Tooltip("The downward speed at which the shotgun dash decends the player")]
    [SerializeField] private float dashDownwardSpeed;
    [Tooltip("Reference to character movement module")]
    [HideInInspector]private CharacterMovementModule character;
    [FormerlySerializedAs("damagecollider")] [Tooltip("Shotgun dash damage collider")]
    public Collider2D damageCollider;
    #endregion

    #region UnityMethods
    /// <summary>
    /// Start is called before the first frame update.
    /// Responsible for setting appropriate SFX key strings and movement module
    /// </summary>
    protected override void Start()
    {
        base.Start();
        weaponName = WeaponAudioStrings.ShotgunName;
        character = Master.GetComponent<CharacterMovementModule>();
    }
    #endregion

    #region Methods
    /// <summary>
    /// Overrides the alt fire function of weapon module - Shotgun Movement Blast
    /// </summary>
    override public void AltFire()
    {
        base.AltFire();
        PushPlayer();
    }
    
    /// <summary>
    /// Sends the player flying as part of the alt fire
    /// </summary>
    private void PushPlayer()
    {
        damageCollider.enabled = true;
        weaponMaster.weaponsAvailable = false;
        playerAnimator.SetTrigger("ShotgunDash");
        Master.r2d.velocity = new Vector2(0, 0);
        Master.gameObject.GetComponent<CharacterMovementModule>().canInput = false;
        // Gets the player mouse position and sends the player in the opposite
        // direction.
        Vector3 dir = transform.position - Camera.main.ScreenToWorldPoint(Input.mousePosition);
        dir.z = 0;
        dir.y = 0;
        dir.Normalize();
        // !IMPORTANT! NEVER set velocity directly. Instead, use AddForce with
        // !ForceMode2D.Impulse. Setting velocity directly causes a race
        // !condition with other things that may be modifying velocity.
        Master.r2d.AddForce(-dir * pushPower, ForceMode2D.Impulse);
        character.isShotgunDashing = true;
        Master.gameObject.layer = 21;
        gameObject.layer = 8;
    }
    
    #endregion


}
