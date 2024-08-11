using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents the Shotgun Trigger Collider for shotgun dash
/// In charge of appropriately damaging enemies that collide
/// <br/>
/// Authors: Suraj Karthikeyan (2023/2024)
/// </summary>
public class ShotgunTriggerDamage : MonoBehaviour
{
    [Tooltip("Reference to character movemement module")]
    [HideInInspector] private CharacterMovementModule character;
    [Tooltip("Reference to shotgun weapon")]
    [SerializeField] private ShotGunWeapon shotgun;
    
    
    /// <summary>
    /// Called on the first frame. Assigns character movememnt module
    /// </summary>
    void Start()
    {
        character = transform.GetComponentInParent<CharacterMovementModule>();
    }

    /// <summary>
    /// On Trigger enter. Ensures collision with enemy & player is shotgun dashing
    /// </summary>
    /// <param name="collision"></param>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Enemy") && character.isShotgunDashing)
        {
            EnemyBasic enemy = collision.gameObject.GetComponent<EnemyBasic>();
            if (enemy != null)
            {
                if (enemy.currentHP <= shotgun.dashDamage)
                {
                    shotgun.dashReset = true;
                    shotgun.altFireDelay.Finish();
                }
                enemy.TakeDamage(shotgun.dashDamage);
            }
        }
    }
}
