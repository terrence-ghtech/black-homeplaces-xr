using System;
using System.Linq;
using BCaT.Production.Shell;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BCaT.EditorTools
{
	public static class OpeningOnboardingHierarchyValidation
	{
		private const float DesktopButtonSpacerHeight = 18f;

		public static void RunDesktop()
		{
			SessionState.SetString("BCaT.PlatformTestMode", "Desktop");
			Validate(quest: false);
		}

		public static void RunQuest()
		{
			SessionState.SetString("BCaT.PlatformTestMode", "QuestSimulated");
			Validate(quest: true);
		}

		private static void Validate(bool quest)
		{
			GameObject gameObject = new GameObject("OpeningOnboardingValidationCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			gameObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
			RectTransform rectTransform = (RectTransform)gameObject.transform;
			rectTransform.sizeDelta = new Vector2(1080f, 690f);
			try
			{
				OpeningInstructionsUi.AddTo(rectTransform, quest, quest ? 19f : 20f, 510f);
				Canvas.ForceUpdateCanvases();
				string text = (quest ? "OpeningInstructions_Quest" : "OpeningInstructions_Desktop");
				string text2 = (quest ? "OpeningInstructions_Desktop" : "OpeningInstructions_Quest");
				Check(Find(rectTransform, text).Length == 1, "one active " + text + " root");
				Check(Find(rectTransform, text2).Length == 0, "no " + text2 + " root");
				string[] array = ((!quest) ? new string[5] { "Move", "Use W, A, S, D or the arrow keys to move.", "Nav", "Press Esc and choose Return to Main Entrance.", "Audio" } : new string[5] { "Move", "Use the left thumbstick to move.", "Grip", "Panels", "Audio" });
				string[] array2 = ((!quest) ? new string[3] { "Grip", "Use the left thumbstick to move.", "Meta Quest recenter" } : new string[5] { "WASD", "Mouse", "Recenter", "Meta Quest recenter", "Press Esc and choose Return to Main Entrance." });
				string[] array3 = array;
				foreach (string text3 in array3)
				{
					Check(HasText(rectTransform, text3), "contains '" + text3 + "'");
				}
				array3 = array2;
				foreach (string text4 in array3)
				{
					Check(!HasText(rectTransform, text4), "does not contain '" + text4 + "'");
				}
				if (!quest)
				{
					array3 = new string[4] { "onboarding_move", "onboarding_look", "onboarding_interact", "onboarding_return" };
					foreach (string text5 in array3)
					{
						Check(HasSprite(rectTransform, text5), "contains desktop icon sprite '" + text5 + "'");
					}
				}
				RectTransform parent = Find(rectTransform, text).Single();
				RectTransform[] array4 = Find(parent, "InstructionColumn");
				Check(array4.Length == 2, $"two instruction columns (got {array4.Length})");
				RectTransform[] array5 = array4;
				foreach (RectTransform rectTransform2 in array5)
				{
					LayoutElement component = rectTransform2.GetComponent<LayoutElement>();
					Check(component != null && component.preferredWidth >= 390f && component.minWidth >= 280f, "column '" + rectTransform2.name + "' has usable preferred/min width");
				}
				RectTransform[] array6 = Find(parent, "Text");
				Check(array6.Length >= 6, $"text columns created (got {array6.Length})");
				array5 = array6;
				foreach (RectTransform rectTransform3 in array5)
				{
					LayoutElement component2 = rectTransform3.GetComponent<LayoutElement>();
					Check(component2 != null && component2.preferredWidth >= 390f && component2.minWidth >= 220f, "text column '" + Path(rectTransform3) + "' cannot collapse");
				}
				if (!quest)
				{
					VerticalLayoutGroup component3 = Find(parent, "Item_Audio").Single().Find("Text").GetComponent<VerticalLayoutGroup>();
					VerticalLayoutGroup component4 = Find(parent, "Item_Navigation").Single().Find("Text").GetComponent<VerticalLayoutGroup>();
					Check(Mathf.Approximately(component3.spacing, 15f), $"desktop Audio heading/body spacing is 15 (got {component3.spacing})");
					Check(Mathf.Approximately(component4.spacing, 3f), $"desktop Navigation spacing remains 3 (got {component4.spacing})");
					ValidateDesktopButtonContained(rectTransform);
				}
				Debug.Log("[OpeningOnboardingHierarchyValidation] PASS " + (quest ? "Quest" : "Desktop") + ": single active platform hierarchy with non-collapsing columns.");
				ExitIfBatch(0);
			}
			catch (Exception ex)
			{
				Debug.LogError(string.Format("[OpeningOnboardingHierarchyValidation] FAIL {0}: {1}\n{2}", quest ? "Quest" : "Desktop", ex.Message, ex));
				ExitIfBatch(1);
				throw;
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		private static RectTransform[] Find(Transform parent, string name)
		{
			return (from t in parent.GetComponentsInChildren<RectTransform>(includeInactive: true)
				where t.name == name
				select t).ToArray();
		}

		private static bool HasText(Transform parent, string text)
		{
			return parent.GetComponentsInChildren<TMP_Text>(includeInactive: true).Any((TMP_Text label) => label != null && label.text != null && label.text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
		}

		private static bool HasSprite(Transform parent, string spriteName)
		{
			return parent.GetComponentsInChildren<Image>(includeInactive: true).Any((Image image) => image != null && image.sprite != null && string.Equals(image.sprite.name, spriteName, StringComparison.Ordinal) && image.sprite.texture != null && image.sprite.texture.width > 1 && image.sprite.texture.height > 1);
		}

		private static void ValidateDesktopButtonContained(Transform parent)
		{
			RectTransform rectTransform = UiFactory.CreateCenterPanel(parent, "Panel", new Vector2(1080f, 690f));
			RectTransform parent2 = UiFactory.CreateColumn(rectTransform, "Column", 18f);
			OpeningInstructionsUi.AddTo(parent2, quest: false, 20f, 510f);
			RectTransform rectTransform2 = UiFactory.CreateRect(parent2, "DesktopButtonSpacer");
			rectTransform2.sizeDelta = new Vector2(0f, 18f);
			LayoutElement layoutElement = rectTransform2.gameObject.AddComponent<LayoutElement>();
			layoutElement.minHeight = 18f;
			layoutElement.preferredHeight = 18f;
			layoutElement.flexibleHeight = 0f;
			Button button = UiFactory.CreateButton(parent2, "Begin Exploring", null, 27f);
			LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
			Canvas.ForceUpdateCanvases();
			Rect rect = rectTransform.rect;
			RectTransform obj = (RectTransform)button.transform;
			Vector3[] array = new Vector3[4];
			obj.GetWorldCorners(array);
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = rectTransform.InverseTransformPoint(array[i]);
			}
			float num = array.Min((Vector3 c) => c.x);
			float num2 = array.Max((Vector3 c) => c.x);
			float num3 = array.Min((Vector3 c) => c.y);
			float num4 = array.Max((Vector3 c) => c.y);
			Check(num >= rect.xMin - 0.5f && num2 <= rect.xMax + 0.5f, $"desktop Begin Exploring left/right inside panel ({num:F1}..{num2:F1})");
			Check(num3 >= rect.yMin - 0.5f && num4 <= rect.yMax + 0.5f, $"desktop Begin Exploring top/bottom inside panel ({num3:F1}..{num4:F1})");
			Check(Mathf.Abs(num - rect.xMin - (rect.xMax - num2)) <= 1f, "desktop Begin Exploring has equal left/right margins");
		}

		private static void Check(bool condition, string message)
		{
			if (!condition)
			{
				throw new InvalidOperationException(message);
			}
			Debug.Log("[OpeningOnboardingHierarchyValidation] OK: " + message);
		}

		private static string Path(Transform transform)
		{
			string text = transform.name;
			while (transform.parent != null)
			{
				transform = transform.parent;
				text = transform.name + "/" + text;
			}
			return text;
		}

		private static void ExitIfBatch(int code)
		{
			if (Application.isBatchMode)
			{
				EditorApplication.Exit(code);
			}
		}
	}
}
