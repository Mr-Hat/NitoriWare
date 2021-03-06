﻿using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System.Collections;

public class MicrogameController : MonoBehaviour
{
	public static MicrogameController instance;
	private static int preserveDebugSpeed = -1;

	[SerializeField]
	private DebugSettings debugSettings;
	[System.Serializable]
	struct DebugSettings
	{
		public bool playMusic, displayCommand, showTimer, timerTick, simulateStartDelay, localizeText;
        public string forceLocalizationLanguage;
		public VoicePlayer.VoiceSet voiceSet;
		[Range(1, StageController.MAX_SPEED)]
		public int speed;
	}


	public UnityEvent onPause, onUnPause;
    [SerializeField]
    private AudioSource sfxSource;

	private MicrogameTraits traits;
	private bool victory, victoryDetermined;
    private bool debugMode;
    private CommandDisplay commandDisplay;
   

	void Awake()
	{
		instance = this;

		string sceneName = gameObject.scene.name;
		if (sceneName.Equals("Template"))
			sceneName = "_Template1";
		traits = MicrogameTraits.findMicrogameTraits(sceneName.Substring(0, sceneName.Length - 1), int.Parse(sceneName.Substring(sceneName.Length - 1, 1)));

        debugMode = GameController.instance == null || GameController.instance.getStartScene() == "Microgame Debug";

        if (debugMode)
		{
            //Debug Mode Awake (scene open by itself)

            if (MicrogameDebugObjects.instance == null)
                SceneManager.LoadScene("Microgame Debug", LoadSceneMode.Additive);
            else
                MicrogameDebugObjects.instance.Reset();

            if (preserveDebugSpeed > -1)
            {
                Debug.Log("Debugging at speed " + preserveDebugSpeed);
                debugSettings.speed = preserveDebugSpeed;
                preserveDebugSpeed = -1;
            }

            StageController.beatLength = 60f / 130f;
            Time.timeScale = StageController.getSpeedMult(debugSettings.speed);

            victory = traits.defaultVictory;
            victoryDetermined = false;

            traits.onAccessInStage(sceneName.Substring(0, sceneName.Length - 1));
        }
		else if (!isBeingDiscarded())
		{
			//Normal Awake

			StageController.instance.stageCamera.tag = "Camera";
			Camera.main.GetComponent<AudioListener>().enabled = false;

			StageController.instance.microgameMusicSource.clip = traits.musicClip;

			if (traits.hideCursor)
				Cursor.visible = false;

			commandDisplay = StageController.instance.transform.root.Find("UI").Find("Command").GetComponent<CommandDisplay>();

			StageController.instance.resetVictory();
			StageController.instance.onMicrogameAwake();
		}

	}

	void Start()
	{
		if (isBeingDiscarded())
			shutDownMicrogame();
		else
        {
            if (debugMode)
            {
                //Debug Start
                MicrogameDebugObjects debugObjects  = MicrogameDebugObjects.instance;
                commandDisplay = debugObjects.commandDisplay;
                
                if (debugSettings.localizeText)
                {
                    LocalizationManager manager = GameController.instance.transform.Find("Localization").GetComponent<LocalizationManager>();
                    if (!string.IsNullOrEmpty(debugSettings.forceLocalizationLanguage))
                        manager.setForcedLanguage(debugSettings.forceLocalizationLanguage);
                    manager.gameObject.SetActive(true);
                }
                
                MicrogameTimer.instance.beatsLeft = (float)traits.getDurationInBeats() + (debugSettings.simulateStartDelay ? 1f : 0f);
                if (!debugSettings.showTimer)
                    MicrogameTimer.instance.disableDisplay = true;
                if (debugSettings.timerTick)
                    MicrogameTimer.instance.invokeTick();

                if (debugSettings.playMusic && traits.musicClip != null)
                {
                    AudioSource source = debugObjects.musicSource;
                    source.clip = traits.musicClip;
                    source.pitch = StageController.getSpeedMult(debugSettings.speed);
                    if (!debugSettings.simulateStartDelay)
                        source.Play();
                    else
                        AudioHelper.playScheduled(source, StageController.beatLength);
                }
                
                if (debugSettings.displayCommand)
                    debugObjects.commandDisplay.play(traits.localizedCommand);

                Cursor.visible = traits.controlScheme == MicrogameTraits.ControlScheme.Mouse && !traits.hideCursor;
                //Cursor.lockState = CursorLockMode.Confined;

                debugObjects.voicePlayer.loadClips(debugSettings.voiceSet);

            }
            SceneManager.SetActiveScene(gameObject.scene);
        }
	}

	/// <summary>
	/// Disables all root objects in microgame
	/// </summary>
	public void shutDownMicrogame()
	{
		GameObject[] rootObjects = gameObject.scene.GetRootGameObjects();
		for (int i = 0; i < rootObjects.Length; i++)
		{
			rootObjects[i].SetActive(false);
		}
	}

	bool isBeingDiscarded()
	{
        if (debugMode)
            return false;
		return StageController.instance == null
            || StageController.instance.animationPart == StageController.AnimationPart.GameOver
            || StageController.instance.animationPart == StageController.AnimationPart.WonStage;
	}

	/// <summary>
	/// Returns MicrogameTraits for the microgame (from the prefab)
	/// </summary>
	/// <returns></returns>
	public MicrogameTraits getTraits()
	{
		return traits;
	}

	/// <summary>
	/// Returns true if microgame is in debug mode (scene open by itself)
	/// </summary>
	/// <returns></returns>
	public bool isDebugMode()
	{
        return debugMode;
	}

    string getSceneWithoutNumber(string scene)
    {
        return scene.Substring(0, scene.Length - 1);
    }
    
    public AudioSource getSFXSource()
    {
        return sfxSource;
    }

	/// <summary>
	/// Call this to have the player win/lose a microgame, set final to true if the victory status will NOT be changed again
	/// </summary>
	/// <param name="victory"></param>
	/// <param name="final"></param>
	public void setVictory(bool victory, bool final)
	{
		if (debugMode)
		{
			//Debug victory
			if (victoryDetermined)
			{
				return;
			}
			this.victory = victory;
			victoryDetermined = final;
			if (final)
				MicrogameDebugObjects.instance.voicePlayer.playClip(victory, victory ? traits.victoryVoiceDelay : traits.failureVoiceDelay);
		}
		else
		{
			//StageController handles regular victory
			StageController.instance.setMicrogameVictory(victory, final);
		}
	}
	
	/// <summary>
	/// Returns whether the game would be won if it ends now
	/// </summary>
	/// <returns></returns>
	public bool getVictory()
	{
		if (debugMode)
		{
			return victory;
		}
		else
			return StageController.instance.getMicrogameVictory();
	}

	/// <summary>
	/// Returns true if the game's victory outcome will not be changed for the rest of its duration
	/// </summary>
	/// <returns></returns>
	public bool getVictoryDetermined()
	{
		if (debugMode)
		{
			return victoryDetermined;
		}
		else
			return StageController.instance.getVictoryDetermined();
	}

	/// <summary>
	/// Re-displays the command text with the specified message. Only use this if the text will not need to be localized
	/// </summary>
	/// <param name="command"></param>
	public void displayCommand(string command)
	{
		if (!commandDisplay.gameObject.activeInHierarchy)
			commandDisplay.gameObject.SetActive(true);

        commandDisplay.play(command);
	}

    /// <summary>
    /// Gets the currently active command display
    /// </summary>
    /// <returns></returns>
    public CommandDisplay getCommandDisplay()
    {
        return commandDisplay;
    }

	/// <summary>
	/// Re-displays the command text with a localized message. Key is automatically prefixed with "microgame.[ID]."
	/// </summary>
	/// <param name="command"></param>
	public void displayLocalizedCommand(string key, string defaultString)
	{
		displayCommand(TextHelper.getLocalizedMicrogameText(key, defaultString));
	}

    /// <summary>
    /// Plays sound effect unaffected by microgame speed
    /// </summary>
    /// <param name="clip"></param>
    /// <param name="panStereo"></param>
    /// <param name="pitch"></param>
    /// <param name="volume"></param>
    public void playSFXUnscaled(AudioClip clip, float panStereo = 0f, float pitch = 1f, float volume = 1f)
    {
        sfxSource.pitch = pitch;
        sfxSource.panStereo = panStereo;
        sfxSource.PlayOneShot(clip, volume * PrefsHelper.getVolume(PrefsHelper.VolumeType.SFX));
    }

    /// <summary>
    /// Plays sound effect and scales it with current speed. use this for most microgame sounds.
    /// </summary>
    /// <param name="clip"></param>
    /// <param name="panStero"></param>
    /// <param name="pitchMult"></param>
    /// <param name="volume"></param>
    public void playSFX(AudioClip clip, float panStero = 0f, float pitchMult = 1f, float volume = 1f)
    {
        playSFXUnscaled(clip, panStero, pitchMult * Time.timeScale, volume);
    }

	void Update ()
	{
		if (debugMode)
		{
			if (Input.GetKeyDown(KeyCode.R))
				SceneManager.LoadScene(gameObject.scene.buildIndex);
			else if (Input.GetKeyDown(KeyCode.F))
			{
				preserveDebugSpeed = Mathf.Min(debugSettings.speed + 1, StageController.MAX_SPEED);
				SceneManager.LoadScene(gameObject.scene.buildIndex);
			}
			else if (Input.GetKeyDown(KeyCode.N))
			{
				string sceneName = SceneManager.GetActiveScene().name;
				char[] sceneChars = sceneName.ToCharArray();
				if (sceneChars[sceneChars.Length - 1] != '3')
				{
					int stageNumber = int.Parse(sceneChars[sceneChars.Length - 1].ToString());
					sceneName = sceneName.Substring(0, sceneName.Length - 1);
					SceneManager.LoadScene(sceneName + (stageNumber + 1).ToString());
				}
				else
					SceneManager.LoadScene(gameObject.scene.buildIndex);
			}
		}
	}
}
