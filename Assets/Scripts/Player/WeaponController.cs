using Mirror;
using UI;
using UnityEngine;
using Utils;

namespace Player
{
	public class WeaponController : NetworkBehaviour
	{

		// TODO: Сделать комментарии

		private UIObjectsLinks _ui;

		private Transform _camera;
		
		// Оружие
		private bool _isReloading;
		private bool _isFire;
		private float _fireRate;

		private float _reloadTime;
		
		private byte _fullAmmo;
		private byte _ammo;

		private byte _damage;
		
		private Player _player;
		
		public GameObject gun;
		public GameObject muzzleFlashPrefab;
		public Transform muzzleFlashPosition;
		
		public GameObject bulletHolePrefab;
		public GameObject bloodPrefab;

		public GameObject bulletParticlePrefab;
		
		private Animator _gunAnimator;

		private RaycastHit _hit;

		private BulletFlyBySoundSpawner _bulletFlyBySoundSpawner;

		private HitMarkerController _hitMarkerController;
		private HitSoundsController _hitSoundsController;
		
		// Звуки
		[Header("Звуки")] 
		private AudioSource _playerFX;
		public AudioSource[] gunShot;
		
		private bool _isFireLightAllowed;
		

		private void Start()
		{
			GetComponents();
			StartWeapon();
			CheckPlayerPrefsKeys();
			
			_isFireLightAllowed = PlayerPrefsBoolean.GetBool("LightSettings:IsFireLightAllowed");
		}

		private static void CheckPlayerPrefsKeys()
		{
			if (!PlayerPrefs.HasKey("Kills"))
				PlayerPrefs.SetInt("Kills", 0);
		}
		
		private void GetComponents()
		{
			_player = GetComponent<Player>();

			_playerFX = GetComponent<AudioSource>();
			
			_ui = FindObjectOfType<UIObjectsLinks>();
			
			_camera = GameObject.FindGameObjectWithTag("MainCamera").transform;

			_bulletFlyBySoundSpawner = GetComponent<BulletFlyBySoundSpawner>();
			
			_gunAnimator = gun.GetComponent<Animator>();

			_hitMarkerController = GetComponent<HitMarkerController>();

			_hitSoundsController = GetComponent<HitSoundsController>();
		}
		
		public void StartWeapon()
		{
			// TODO: Сделать определение значений переменных автоматически под каждое оружие
			_fullAmmo = 30;
			_ammo = _fullAmmo;
			_reloadTime = 1.5f;
			_fireRate = 0.08f;
			_damage = 15;
			
			_ui.fullAmmoText.text = _fullAmmo.ToString();
			_ui.ammoText.text = _ammo.ToString();
		}
		
		private void Update()
		{
			if(RealInput.IsTouchSupported) return;
			
			if (Input.GetButton("Fire1"))
				Fire();
			
			if (Input.GetKeyDown("r"))
				Reload();
		}

		public void OnFireButtonDown()
		{
			InvokeRepeating(nameof(Fire), 0.0001f, 0.0001f);
		}

		public void OnFireButtonUp()
		{
			CancelInvoke(nameof(Fire));
		}
		
		private void Fire()
		{
			if(!isOwned) return;
			if(_isReloading) return;
			if(_isFire) return;
			if(_player.isDeath) return;
			if(_ui.menu.isPaused) return;
			if (_ammo <= 0)
			{
				Reload();
				return;
			}
			
			CmdSpawnMuzzleFlashPrefab();
			
			_isFire = true;
			
			// Звук
			PlayAudioSources(gunShot);
			
			// Луч
			CastRayCast(_camera.position, _camera.forward);
			
			// Спавн обьекта с партиклами летящей пули
			CmdSpawnBulletParticlePrefab(muzzleFlashPosition.position, Vector3.zero);
			
			_ammo -= 1;
			_ui.ammoText.text = _ammo.ToString();
			
			_gunAnimator.Play("Fire");
			
			Invoke(nameof(StopFire), _fireRate);
		}

		private void BreakingThrough(Vector3 direction, byte damageModifier)
		{
			var adjustedPoint = _hit.point + _camera.forward / 10; // Отодвигаем точку на 1 вперед по направлению рэйкаста
			_damage -= damageModifier;
			CastRayCast(adjustedPoint, direction);
		}
		
		private void CastRayCast(Vector3 origin, Vector3 direction)
		{
			if (!Physics.Raycast(origin, direction, out _hit, Mathf.Infinity,
				    Physics.DefaultRaycastLayers)) return;
			// ReSharper disable all Unity.PerformanceCriticalCodeInvocation

			if (_hit.collider.CompareTag("Glass"))
			{
				_hit.collider.GetComponent<BreakableWindow>().CmdBreakWindow();
				BreakingThrough(direction, 1);
				return;
			}

			if(_hit.collider.CompareTag("Lamp"))
			{
				_hit.collider.GetComponent<Lamp>().CmdBreakLamp();
				BreakingThrough(direction, 2);
				return;
			}

			if (_hit.transform.CompareTag("Player") && !_hit.collider.CompareTag("PlayerBulletFlyBy")) // Если обьект в который попали имеет тэг игрока
			{
				DamagePlayer(_hit, _damage);
				BreakingThrough(direction, 7);
			}

			if (_hit.collider.CompareTag("PlayerBulletFlyBy"))
			{
				_bulletFlyBySoundSpawner.CmdSpawnBulletFlyBySound(_hit.point, new Quaternion());
				
				BreakingThrough(direction, 0);
				return;
			}

			if (_hit.collider.CompareTag("ExplosiveBarrel"))
			{
				CmdSetVelocity(_hit.rigidbody, gameObject.transform.forward * 5);
				_hit.collider.GetComponent<ExplosiveBarrel>().CmdShooted(15, _hit.point, gameObject.transform.position);
			}

			if (_hit.collider.CompareTag("ExplosiveBarrelFragments"))
			{
				_hit.rigidbody.velocity = _camera.forward * 20;
				
				BreakingThrough(direction, 3);
				return;
			}

			if(_hit.collider.CompareTag("PhysicalBody"))
			{
				_hit.rigidbody.velocity = _camera.forward * 20;
				
				BreakingThrough(direction, 5);
				return;
			}

			if (_hit.collider.CompareTag("AirplaneCargo"))
			{
				CmdSetVelocity(_hit.rigidbody, gameObject.transform.forward * 5);
				return;
			}

			if (!_hit.collider.CompareTag("Player") || !_hit.collider.CompareTag("DeadZone") || !_hit.collider.CompareTag("PlayerBulletFlyBy") || !_hit.collider.CompareTag("Glass"))
			{
				CmdSpawnBulletHolePrefab(_hit.point, Quaternion.Euler(Vector3.Angle(_hit.normal, Vector3.up), 0, 0));
				
				// Рикошет
				if (Vector3.Angle(_hit.normal, _camera.forward) <= 105)
				{
					// Луч
					var old_hit_point = _hit.point;
					CastRayCast(_hit.point, Vector3.Reflect(direction, _hit.normal));

					CmdSpawnBulletHolePrefab(_hit.point, Quaternion.Euler(Vector3.Angle(_hit.normal, Vector3.up), 0, 0));
					
					// Спавн обьекта с партиклами летящей пули
					CmdSpawnBulletParticlePrefab(old_hit_point, _hit.point);
				}
			}
		}
        
		public void DamagePlayer(RaycastHit hit, byte damage)
		{
			var shootedPlayer = hit.collider.GetComponent<Player>();
			
			shootedPlayer.CmdChangeHp(damage, transform, _player.playerDisplayName);
			
			CmdSpawnBloodPrefab(hit.point, Quaternion.Euler(Vector3.Angle(hit.normal, Vector3.up), 0, 0));
			
			_hitMarkerController.EnableAndDisableMarker(shootedPlayer.isDeath);
			
			
			if(!shootedPlayer.isDeath) return;
			if (shootedPlayer.isAlreadyDeath) return;

			_hitMarkerController.SetPlayerKilledText(shootedPlayer.playerDisplayName);
			
			_hitSoundsController.PlayHitBassSound();
			_hitSoundsController.PlayHitMarkerSound();
			
			// Звук колокольчика
			_hitSoundsController.PlayBellSound();
			
			PlayerPrefs.SetInt("Kills", PlayerPrefs.GetInt("Kills") + 1);
			print("Kills:" + PlayerPrefs.GetInt("Kills"));
		}
		
		private static void PlayAudioSources(AudioSource[] sounds)
		{
			foreach (var sound in sounds)
			{
				if (!sound.isPlaying)
				{
					sound.Play();
					return;
				}
			}
		}

		#region Network Methods
		[Command (requiresAuthority = false)]
		// ReSharper disable once MemberCanBeMadeStatic.Local
		private void CmdSetVelocity(Rigidbody rigidbody, Vector3 velocity)
		{
			rigidbody.GetComponent<Rigidbody>().velocity = velocity;
		}

		[Command(requiresAuthority = false)]
		private void CmdSpawnBulletParticlePrefab(Vector3 position, Vector3 look)
		{
			var prefab = Instantiate(bulletParticlePrefab, position, muzzleFlashPosition.rotation);
			NetworkServer.Spawn(prefab);
			
			if(look != Vector3.zero)
				prefab.transform.LookAt(look);
		}
		
		[Command (requiresAuthority = false)]
		private void CmdSpawnMuzzleFlashPrefab()
		{
			var prefab = Instantiate(muzzleFlashPrefab, muzzleFlashPosition.position, muzzleFlashPosition.rotation);
			prefab.transform.SetParent(transform);
			NetworkServer.Spawn(prefab);
		}

		[Command (requiresAuthority = false)]
		public void CmdSpawnBulletHolePrefab(Vector3 position, Quaternion rotation)
		{
			var prefab = Instantiate(bulletHolePrefab, position, rotation);
			NetworkServer.Spawn(prefab);
		}
		
		[Command (requiresAuthority = false)]
		private void CmdSpawnBloodPrefab(Vector3 position, Quaternion rotation)
		{
			var prefab = Instantiate(bloodPrefab, position, rotation);
			NetworkServer.Spawn(prefab);
		}
		#endregion
		
		private void StopFire()
		{
			if(!isOwned) return;
			
			_gunAnimator.Play("Idle");
			
			_isFire = false;
			if(_ammo == 0)
				Reload();
		}
		
		
		public void Reload()
		{
			if(!isOwned) return;
			if(_isReloading) return;
			if(_ammo == _fullAmmo) return;
			if(_player.isDeath) return;
			if(_ui.menu.isPaused) return;
			
			_isReloading = true;
			
			_ui.reloadText.SetActive(true);
			
			_gunAnimator.Play("Reload");
			
			Invoke(nameof(StopReload), _reloadTime);
		}

		private void StopReload()
		{
			if(!isOwned) return;
			
			_ammo = _fullAmmo;
			_ui.ammoText.text = _ammo.ToString();
			_ui.reloadText.SetActive(false);
			_isReloading = false;
			_gunAnimator.Play("Idle");
		}
		
	}
}
