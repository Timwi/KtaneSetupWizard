﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using static UnityEngine.Random;
using static UnityEngine.Debug;

public class SetupWizardScript : MonoBehaviour {

	public KMBombInfo Bomb;
	public KMAudio Audio;
	public KMBombModule Module;

	public KMSelectable[] mainButtons, keyboardLetters, keyboardNumbers, folderButtons, accountPrompts, page2Buttons;
	public KMSelectable backSpace, shift, reset, submit;

	public TextMesh[] mainTexts, expressionDisplays;

	public GameObject window;
	public GameObject[] pages;

	public Material[] blackScreens, backgrounds, windowIcons;
	public MeshRenderer windowIcon, screen;

	static int moduleIdCounter = 1;
	int moduleId;
	private bool moduleSolved, isActivated, moduleSelected, canTypeUser, canTypePassword, shiftedLetters = true, canSubmit, canSolve;

	private Folder[] folders =
	{
		new Folder("Downloads", new int[] { 1, 4 }),
		new Folder("Pictures", new int[] { 3, 5 }),
		new Folder("Documents", new int[] { 0, 1 }),
		new Folder("Music", new int[] { 0, 5 }),
		new Folder("Homework", null, 2),
		new Folder("Videos", new int[] { 2, 4 })
	};

	private Folder startingFolder;
	private Folder[] shuffledFolders;

	private ObtainUsername username;

	private Expression[] generatedPuzzle = new Expression[6], modifiedPuzzle = new Expression[6];
	private EquationSystem equationSystem = new EquationSystem();

	private int currentPos, currentPage = 0, page2Ix = 0;

	private string usernameInput = string.Empty, passwordInput = string.Empty, passwordAsterisk = string.Empty;

	private int[] randomIxes = new int[6], passwordDigits;
	private List<string> expressionsToDisplay;

	private string FinalPassword(string s, int count) => s.Substring(6 - count) + s.Substring(0, 6 - count);
	private string finalPass;

	private Folder[] SwapFolders(int[] swaps)
	{
		var foldersSwapped = new Folder[2];

		var foldersToSwap = swaps.Select(x => folders[x].FolderName).ToArray();
		var foldersDirectories = swaps.Select(x => folders[x].Directories).ToArray();
		var foldersSingleDirectory = swaps.Select(x => folders[x].SingleDirectory).ToArray();

		for (int i = 0; i < 2; i++)
		{
			if (foldersDirectories[i] == null)
			{
				foldersSwapped[i] = new Folder(foldersToSwap[i == 0 ? 1 : 0], null, foldersSingleDirectory[i]);
				continue;
			}

			foldersSwapped[i] = new Folder(foldersToSwap[i == 0 ? 1 : 0], foldersDirectories[i]);
		}



		return foldersSwapped;
	}

	private Expression[] SwapAnswers(int[] swaps)
	{
		var swappedAnswers = new Expression[2];

		var answersToSwap = swaps.Select(x => generatedPuzzle[x].Answer).ToArray();
		var expressionALetters = swaps.Select(x => generatedPuzzle[x].NumIxA).ToArray();
		var expressionEquationExp = swaps.Select(x => generatedPuzzle[x].EquationExpression).ToArray();
		var expressionBLetters = swaps.Select(x => generatedPuzzle[x].NumIxB).ToArray();

		for (int i = 0; i < 2; i++)
			swappedAnswers[i] = new Expression(expressionALetters[i], expressionEquationExp[i], expressionBLetters[i], answersToSwap[i == 0 ? 1 : 0]);

		return swappedAnswers;
	}

	private int GetLetterIndex(char c) => "QWERTYUIOPASDFGHJKLZXCVBNM".IndexOf(c);

	void Awake()
	{

		moduleId = moduleIdCounter++;

		foreach (KMSelectable letter in keyboardLetters)
			letter.OnInteract += delegate () { KeyboardLetterPress(letter); return false; };

		foreach (KMSelectable number in keyboardNumbers)
			number.OnInteract += delegate () { KeyboardNumberPress(number); return false; };

		foreach (KMSelectable folder in folderButtons)
			folder.OnInteract += delegate () { FolderPress(folder); return false; };

		foreach (KMSelectable mainButton in mainButtons)
			mainButton.OnInteract += delegate () { MainButtonPress(mainButton); return false; };

		foreach (KMSelectable prompt in accountPrompts)
			prompt.OnInteract += delegate () { AccountPromptPress(prompt); return false; };

		foreach (KMSelectable pg in page2Buttons)
			pg.OnInteract += delegate () { Page2Press(pg); return false; };



		reset.OnInteract += delegate () { StopAllCoroutines(); StartCoroutine(ResetPress()); return false; };
		backSpace.OnInteract += delegate () { BackSpacePress(); return false; };
		shift.OnInteract += delegate () { ShiftPress(); return false; };
		submit.OnInteract += delegate () { SubmitPress(); return false; };


		Module.OnActivate += delegate () { StopAllCoroutines(); StartCoroutine(Startup()); };
		Module.GetComponent<KMSelectable>().OnFocus += delegate { moduleSelected = true; };
		Module.GetComponent<KMSelectable>().OnDefocus += delegate { moduleSelected = false; };

	}


	void Start()
	{
		foreach (var obj in pages)
			obj.SetActive(false);

		submit.gameObject.SetActive(false);

		window.SetActive(false);

		StartCoroutine(Initialize());


		currentPos = Range(0, 6);

		shuffledFolders = folders;

		var foldersToShuffle = Enumerable.Range(0, 6).ToList().Shuffle().Take(2).ToArray();

		var swappedFolders = SwapFolders(foldersToShuffle);

		for (int i = 0; i < 2; i++)
			shuffledFolders[foldersToShuffle[i]] = swappedFolders[i];

		startingFolder = folders[currentPos];

		username = new ObtainUsername(foldersToShuffle.Select(x => folders[x]).ToArray(), folders, startingFolder);

		Log($"[Setup Wizard #{moduleId}] The starting folder for step 1 is: {startingFolder.FolderName}");
		Log($"[Setup Wizard #{moduleId}] The swapped folders for step 1 were: {foldersToShuffle.Select(x => folders[x].FolderName).Join(", ")}");
		Log($"[Setup Wizard #{moduleId}] The username should be {username.GetUsername(Bomb)}");

		GeneratePassword();

	}

	void GeneratePassword()
	{
		tryagain:

        passwordDigits = equationSystem.GeneratedPassword();

        for (int i = 0; i < 6; i++)
            randomIxes[i] = Range(0, 5);


        generatedPuzzle = equationSystem.GeneratedPuzzle(passwordDigits, randomIxes);
        modifiedPuzzle = generatedPuzzle;

        var answersToShuffle = Enumerable.Range(0, 6).ToList().Shuffle().Take(2).ToArray();

        var shuffledAnswers = SwapAnswers(answersToShuffle);

        for (int i = 0; i < 2; i++)
            modifiedPuzzle[answersToShuffle[i]] = shuffledAnswers[i];

        expressionsToDisplay = Enumerable.Range(0, 6).Select(x => $"{"a),b),c),d),e),f)".Split(',')[x]} {modifiedPuzzle[x].NumIxA} {modifiedPuzzle[x].EquationExpression} {modifiedPuzzle[x].NumIxB} = {(modifiedPuzzle[x].EquationExpression == "||" ? modifiedPuzzle[x].Answer.ToString("00") : modifiedPuzzle[x].Answer.ToString())}").ToList();

       

        var obtainFinalLetter = ((Bomb.GetSerialNumberNumbers().Last() % 5) + 1) - 1;

        var obtainSwaps = answersToShuffle.Select(x => generatedPuzzle[x].Answer).ToArray();

        var minValue = Math.Min(obtainSwaps[0], obtainSwaps[1]);
        var maxValue = Math.Max(obtainSwaps[0], obtainSwaps[1]);

		if ((minValue == 0 && randomIxes[obtainFinalLetter] == 3) || (minValue < 0 && randomIxes[obtainFinalLetter] == 4))
			goto tryagain;

		if (equationSystem.Equation(randomIxes[obtainFinalLetter], maxValue, minValue) < 0)
			goto tryagain;

        finalPass = FinalPassword(passwordDigits.Join(""), equationSystem.Equation(randomIxes[obtainFinalLetter], maxValue, minValue) % 6);

        Log($"[Setup Wizard #{moduleId}] The password unmodified is: {passwordDigits.Join("")}");
        Log($"[Setup Wizard #{moduleId}] *====================*");

        foreach (var expression in expressionsToDisplay)
            Log($"Setup Wizard #{moduleId}] {expression}");

        Log($"[Setup Wizard #{moduleId}] *====================*");

        Log($"[Setup Wizard #{moduleId}] The swapped numbers for step 2 were: {answersToShuffle.Select(x => generatedPuzzle[x].Answer).Join(", ")}, which where in {answersToShuffle.Select(x => "a),b),c),d),e),f)".Split(',')[x]).Join(", ")}");

        Log($"[Setup Wizard #{moduleId}] The final password after shifting is: {finalPass}");
    }

	void AccountPromptPress(KMSelectable prompt)
	{
		prompt.AddInteractionPunch(0.4f);
		Audio.PlaySoundAtTransform("Click", transform);

		if (moduleSolved || !isActivated || !canSubmit)
			return;

		switch (Array.IndexOf(accountPrompts, prompt))
		{
			case 0:

				if (canTypeUser)
					return;

				canTypePassword = false;

				canTypeUser = true;
				break;
			case 1:
				if (canTypePassword)
					return;

				canTypeUser = false;

				canTypePassword = true;
				break;
		}
	}

	void MainButtonPress(KMSelectable button)
	{
		button.AddInteractionPunch(0.4f);
		Audio.PlaySoundAtTransform("Click", transform);

		if (moduleSolved || !isActivated)
			return;

		switch (Array.IndexOf(mainButtons, button))
		{
			case 0:
				switch (currentPage)
				{
					case 0:
						return;
					case 3:
						canSubmit = false;
						goto default;
					default:
						currentPage--;
						break;
				}
				break;
			case 1:
				switch (currentPage)
				{
					default:
						currentPage++;
						break;
					case 0:
						FolderUpdate();
						goto default;
					case 1:
						Page2Update();
						goto default;
					case 2:
						canSubmit = true;
						goto default;
					case 3:
						if (usernameInput == username.GetUsername(Bomb) && passwordInput == finalPass)
						{
							Log($"[Setup Wizard #{moduleId}] The username and password are correct. The setup is now finished and the application is running.");
							currentPage++;
							canSolve = true;
							canSubmit = false;
                        }
						else
						{
							var result = new List<string>();

							if (usernameInput != username.GetUsername(Bomb))
								result.Add(usernameInput.Length > 0 ? $"(Expected username is {username.GetUsername(Bomb)}, but inputted {usernameInput})" : "(Username input is empty)");

							if (passwordInput != finalPass)
								result.Add(passwordInput.Length > 0 ? $"(Expected password is {finalPass}, but inputted {passwordInput})" : "(Password input is empty)");

							Log($"[Setup Wizard #{moduleId}] Either the username or password is invalid {result.Join()}. Strike!");
							Module.HandleStrike();
						}
						break;
					case 4:
						return;
				}
				break;
		}

		WindowUpdate();
	}

	void WindowUpdate()
	{
		var mainText = mainButtons[1].GetComponentInChildren<TextMesh>().text;

		mainButtons[1].GetComponentInChildren<TextMesh>().text = currentPage == 3 ? "Finish" : mainText;

		Color32[] grayed = { new Color32(0, 0, 0, 60), Color.black };

		mainButtons[0].GetComponentInChildren<TextMesh>().color = currentPage == 0 ? grayed[0] : grayed[1];

		if (currentPage == 4)
		{
			foreach (KMSelectable mainButton in mainButtons)
				mainButton.gameObject.SetActive(false);
			reset.gameObject.SetActive(false);

			foreach (var obj in pages)
				obj.SetActive(false);

			windowIcon.material = windowIcons[1];
			submit.gameObject.SetActive(true);

			return;
		}

		

		for (int i = 0; i < pages.Length; i++)
			pages[i].SetActive(i == currentPage);
	}

	void FolderPress(KMSelectable folder)
	{
		folder.AddInteractionPunch(0.4f);
		Audio.PlaySoundAtTransform("Click", transform);

		if (moduleSolved || !isActivated || currentPage != 1)
			return;

		var ix = Array.IndexOf(folderButtons, folder);

		currentPos = shuffledFolders[currentPos].Directories == null ? shuffledFolders[currentPos].SingleDirectory.Value : shuffledFolders[currentPos].Directories[ix];

		FolderUpdate();
	}

	void FolderUpdate()
	{

		if (shuffledFolders[currentPos].Directories == null)
		{
			folderButtons[1].gameObject.SetActive(false);
			folderButtons[0].GetComponentInChildren<TextMesh>().text = shuffledFolders[folders[currentPos].SingleDirectory.Value].FolderName;
		}
		else
		{
			folderButtons[1].gameObject.SetActive(true);

			var folderNames = shuffledFolders[currentPos].Directories.Select(x => folders[x].FolderName).ToArray();

			for (int i = 0; i < 2; i++)
				folderButtons[i].GetComponentInChildren<TextMesh>().text = folderNames[i];
		}

	}

	void Page2Update()
	{
		for (int i = 0; i < 4; i++)
		{
			var ix = i + page2Ix;
			expressionDisplays[i].text = expressionsToDisplay[ix];
        }
			
	}

	void Page2Press(KMSelectable pg)
	{
		pg.AddInteractionPunch(0.4f);
		Audio.PlaySoundAtTransform("Click", transform);

		if (moduleSolved || !isActivated)
			return;

		switch(Array.IndexOf(page2Buttons, pg))
		{
			case 0:
				if (page2Ix == 0)
					return;
				page2Ix--;
				Page2Update();
				break;
			case 1:
				if (page2Ix == 2)
					return;
				page2Ix++;
				Page2Update();
				break;
		}
	}


	void KeyboardLetterPress(KMSelectable letter)
	{
		letter.AddInteractionPunch(0.4f);
		Audio.PlaySoundAtTransform("KeyPress", transform);

		if (moduleSolved || !isActivated || !canSubmit && !canTypeUser)
			return;

		if (usernameInput.Length < username.GetUsername(Bomb).Length)
		{
            usernameInput += letter.GetComponentInChildren<TextMesh>().text;
			accountPrompts[0].GetComponentInChildren<TextMesh>().text = usernameInput;
        }
			
	}

	void KeyboardNumberPress(KMSelectable number)
	{
		number.AddInteractionPunch(0.4f);
		Audio.PlaySoundAtTransform("KeyPress", transform);

		if (moduleSolved || !isActivated || !canSubmit && !canTypePassword)
			return;

		if (passwordInput.Length < 6)
		{
            passwordInput += number.GetComponentInChildren<TextMesh>().text;
			passwordAsterisk += '*';
			accountPrompts[1].GetComponentInChildren<TextMesh>().text = passwordAsterisk;
        }
			
	}

	void ShiftPress()
	{
		shift.AddInteractionPunch(0.4f);
		Audio.PlaySoundAtTransform("KeyPress", transform);

		if (moduleSolved || !isActivated || !canSubmit)
			return;

		shiftedLetters = !shiftedLetters;

		var letters = "QWERTYUIOPASDFGHJKLZXCVBNM";

		for (int i = 0; i < 26; i++)
			keyboardLetters[i].GetComponentInChildren<TextMesh>().text = shiftedLetters ? letters[i].ToString().ToUpperInvariant() : letters[i].ToString().ToLowerInvariant();
	}

	void SubmitPress()
	{
		submit.AddInteractionPunch(0.4f);
		Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);

		if (moduleSolved || !isActivated || !canSolve)
			return;

		Audio.PlaySoundAtTransform("Solve", transform);

		Log($"[Setup Wizard #{moduleSolved}] The button has been pressed. Solved!");

		moduleSolved = true;
		Module.HandlePass();
	}

	IEnumerator ResetPress()
	{
		reset.AddInteractionPunch(0.4f);
		Audio.PlaySoundAtTransform("Click", transform);

		if (moduleSolved || !isActivated || currentPage == 0)
			yield break;

		currentPage = 0;
		currentPos = Array.IndexOf(folders, startingFolder);
		window.SetActive(false);
		isActivated = false;
		WindowUpdate();

		yield return new WaitForSeconds(1);

		Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.TitleMenuPressed, transform);
		isActivated = true;
		window.SetActive(true);
	}

	void BackSpacePress()
	{
		backSpace.AddInteractionPunch(0.4f);
		Audio.PlaySoundAtTransform("KeyPress", transform);

		if (moduleSolved || !isActivated || !canSubmit)
			return;

		if (canTypeUser)
			if (usernameInput.Length > 0)
			{
				usernameInput = usernameInput.Remove(usernameInput.Length - 1);
                accountPrompts[0].GetComponentInChildren<TextMesh>().text = usernameInput;
            }
		
		if (canTypePassword)
            if (passwordInput.Length > 0 && passwordAsterisk.Length > 0)
            {
                passwordInput = passwordInput.Remove(passwordInput.Length - 1);
                passwordAsterisk = passwordAsterisk.Remove(passwordAsterisk.Length - 1);
                accountPrompts[1].GetComponentInChildren<TextMesh>().text = passwordAsterisk;
            }
    }

	IEnumerator Startup()
	{
		Audio.PlaySoundAtTransform("Window Setup", transform);
		screen.material = backgrounds.PickRandom();

		yield return new WaitForSeconds(1);

		WindowUpdate();
		window.SetActive(true);
		pages[0].SetActive(true);
		isActivated = true;

	}

	IEnumerator Initialize()
	{
		yield return new WaitForSeconds(0.5f);
		screen.material = blackScreens[1];

	}
	
	
	void Update()
    {
		if (moduleSolved || !isActivated)
			return;

		if (moduleSelected && canTypeUser || canTypePassword)
		{
			for (int ltr = 0; ltr < 26; ltr++)
				if (Input.GetKeyDown(((char)('a' + ltr)).ToString()))
				{
					keyboardLetters[GetLetterIndex((char)('A' + ltr))].OnInteract();
					return;
				}

			var validNumPresses = new[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0 };

			for (int num = 0; num < validNumPresses.Length; num++)
				if (Input.GetKeyDown(validNumPresses[num]))
				{
					keyboardNumbers[num].OnInteract();
					return;
				}

			if (Input.GetKeyDown(KeyCode.Backspace))
				backSpace.OnInteract();

			if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
				shift.OnInteract();


		}

    }

	// Twitch Plays


#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"!{0} back/next goes to the previous/next page. || !{0} reset resets back to the original state. || !{0} folder 1/2 navigates to said folder. || !{0} page up/down to navigate through the system of equations. || !{0} username [input] types down the username you want to input. || !{0} password [input] types down the password you want to input. || !{0} done solves the module once the green defuse button appears.";
#pragma warning restore 414

	IEnumerator ProcessTwitchCommand(string command)
    {
		string[] split = command.ToUpperInvariant().Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
		yield return null;

		if ("BACK".ContainsIgnoreCase(split[0]))
		{
			if (split.Length > 1)
				yield break;

			if (currentPage == 0)
			{
				yield return "sendtochaterror You cannot go back any further!";
				yield break;
			}

			mainButtons[0].OnInteract();
			yield return new WaitForSeconds(0.1f);
			yield break;
		}

		if ("NEXT".ContainsIgnoreCase(split[0]))
		{
			if (split.Length > 1)
				yield break;

			if (currentPage == 4)
			{
				yield return "sendtochaterror You cannot go any further!";
				yield break;
			}

			mainButtons[1].OnInteract();
			yield return new WaitForSeconds(0.1f);
			yield break;
		}

		if ("FOLDER".ContainsIgnoreCase(split[0]))
		{
			if (currentPage != 1)
			{
				yield return "sendtochaterror You are not at step 1!";
				yield break;
			}

			if (split.Length == 1)
			{
				yield return "sendtochaterror Please follow it up with either 1 or 2!";
				yield break;
			}

			if (split[1].Length > 1 || split.Length > 2)
				yield break;

			if (!"12".Contains(split[1]))
			{
				yield return $"sendtochaterror {split[1]} is not 1 or 2!";
				yield break;
			}

			if (split[1] == "2" && folderButtons[1].gameObject.activeSelf)
			{
				yield return "sendtochaterror Folder 2 isn't available currently!";
				yield break;
			}

			folderButtons[int.Parse(split[1]) - 1].OnInteract();
			yield return new WaitForSeconds(0.1f);
			yield break;
		}

		if ("PAGE".ContainsIgnoreCase(split[0]))
		{
			if (currentPage != 2)
			{
				yield return "sendtochaterror You are not at step 2!";
				yield break;
			}

			if (split.Length == 1)
			{
				yield return "sendtochaterror Please specify whether you want to go up or down!";
				yield break;
			}

			if (split.Length > 2)
				yield break;

			var validPg = new[] { "UP", "DOWN" };


            if (!validPg.Contains(split[1]))
			{
				yield return $"sendtochaterror {split[1]} is not valid!";
				yield break;
			}

			var press = Array.IndexOf(validPg, split[1]);

			if (press == 0 && page2Ix == 0)
			{
				yield return "sendtochaterror You cannot go further up!";
				yield break;
			}

			if (press == 1 && page2Ix == 2)
			{
				yield return "sendtochaterror You cannot go further down!";
				yield break;
			}


            page2Buttons[press].OnInteract();
			yield return new WaitForSeconds(0.1f);

			yield break;
		}

		if ("USERNAME".ContainsIgnoreCase(split[1]))
		{
			if (currentPage != 3)
			{
				yield return "sendtochaterror You are not at step 3!";
				yield break;
			}

			if (split.Length == 1)
			{
				yield return "sendtochaterror Please input your username!";
				yield break;
			}

			if (split[1].Length > username.GetUsername(Bomb).Length)
				yield break;

			if (!split[1].All(x => char.IsLetter(x)))
			{
				yield return "sendtochaterror Make sure the username you're inputting doesn't contain numbers!";
				yield break;
			}

			if (!canTypeUser)
			{
				accountPrompts[0].OnInteract();
				yield return new WaitForSeconds(0.1f);
			}

			while (usernameInput.Length != 0)
			{
				backSpace.OnInteract();
				yield return new WaitForSeconds(0.1f);
			}

			foreach (var letter in split[1])
			{
				if ((char.IsUpper(letter) && !shiftedLetters) || (char.IsLower(letter) && shiftedLetters))
				{
					shift.OnInteract();
					yield return new WaitForSeconds(0.1f);
				}

				keyboardLetters[GetLetterIndex(char.ToUpperInvariant(letter))].OnInteract();
				yield return new WaitForSeconds(0.1f);
			}

			yield break;
		}

		if ("PASSWORD".ContainsIgnoreCase(split[0]))
		{
            if (currentPage != 3)
            {
                yield return "sendtochaterror You are not at step 3!";
                yield break;
            }

            if (split.Length == 1)
            {
                yield return "sendtochaterror Please input your password!";
                yield break;
            }

			if (split[1].Length > 6)
				yield break;

			if (!split[1].All(x => char.IsNumber(x)))
			{
				yield return "sendtochaterror Your password input contains letters rather than all digits!";
				yield break;
			}

			if (!canTypePassword)
			{
				accountPrompts[1].OnInteract();
				yield return new WaitForSeconds(0.1f);
			}

			while (passwordInput.Length != 0)
			{
				backSpace.OnInteract();
				yield return new WaitForSeconds(0.1f);
			}

			foreach (var num in split[1].Select(x => "1234567890".IndexOf(x)).ToArray())
			{
				keyboardNumbers[num].OnInteract();
				yield return new WaitForSeconds(0.1f);
			}
        }
		
    }

	IEnumerator TwitchHandleForcedSolve()
    {
		yield return null;
    }


}





