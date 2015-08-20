﻿/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace MatterHackers.GuiAutomation
{
	public class AutomationRunner
	{
		public long MatchLimit = 50;
		/// <summary>
		/// The number of seconds to move the mouse when going to a new position.
		/// </summary>
		public double TimeToMoveMouse = .5;

		private string imageDirectory;
		private double upDelaySeconds = .2;

		public AutomationRunner(string imageDirectory)
		{
			this.imageDirectory = imageDirectory;
		}

		public enum ClickOrigin { LowerLeft, Center };

		public enum InterpolationType { LINEAR, EASE_IN, EASE_OUT, EASE_IN_OUT };

		#region Utility
		public Point2D CurrentMousPosition()
		{
			Point2D mousePos = new Point2D(System.Windows.Forms.Control.MousePosition.X, System.Windows.Forms.Control.MousePosition.Y);
			return mousePos;
		}

		public ImageBuffer GetCurrentScreen()
		{
			return NativeMethods.GetCurrentScreen();
		}

		public double GetInterpolatedValue(double compleatedRatio0To1, InterpolationType interpolationType)
		{
			switch (interpolationType)
			{
				case InterpolationType.LINEAR:
					return compleatedRatio0To1;

				case InterpolationType.EASE_IN:
					return Math.Pow(compleatedRatio0To1, 3);

				case InterpolationType.EASE_OUT:
					return (Math.Pow(compleatedRatio0To1 - 1, 3) + 1);

				case InterpolationType.EASE_IN_OUT:
					if (compleatedRatio0To1 < .5)
					{
						return Math.Pow(compleatedRatio0To1 * 2, 3) / 2;
					}
					else
					{
						return (Math.Pow(compleatedRatio0To1 * 2 - 2, 3) + 2) / 2;
					}

				default:
					throw new NotImplementedException();
			}
		}
		#endregion Utility

		#region Mouse Functions
		#region Search By Image

		public bool ClickImage(string imageName, int xOffset = 0, int yOffset = 0, ClickOrigin origin = ClickOrigin.Center, SearchRegion searchRegion = null, MouseButtons mouseButtons = MouseButtons.Left)
		{
			ImageBuffer imageToLookFor = LoadImageFromSourcFolder(imageName);
			if (imageToLookFor != null)
			{
				return ClickImage(imageToLookFor, xOffset, yOffset, origin, searchRegion, mouseButtons);
			}

			return false;
		}

		public bool ClickImage(ImageBuffer imageNeedle, int xOffset = 0, int yOffset = 0, ClickOrigin origin = ClickOrigin.Center, SearchRegion searchRegion = null, MouseButtons mouseButtons = MouseButtons.Left)
		{
			if (origin == ClickOrigin.Center)
			{
				xOffset += imageNeedle.Width / 2;
				yOffset += imageNeedle.Height / 2;
			}

			if (searchRegion == null)
			{
				searchRegion = GetScreenRegion();
			}

			Vector2 matchPosition;
			double bestMatch;
			if (searchRegion.Image.FindLeastSquaresMatch(imageNeedle, out matchPosition, out bestMatch, MatchLimit))
			{
				int screenHeight = NativeMethods.GetCurrentScreenHeight();
				int clickY = (int)(searchRegion.ScreenRect.Bottom + matchPosition.y + yOffset);
				int clickYOnScreen = screenHeight - clickY; // invert to put it on the screen

				Point2D screenPosition = new Point2D((int)matchPosition.x + xOffset, clickYOnScreen);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				switch (mouseButtons)
				{
					case MouseButtons.None:
						break;
					case MouseButtons.Left:
						NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 0, 0);
						Wait(upDelaySeconds);
						NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, screenPosition.x, screenPosition.y, 0, 0);
						break;
					case MouseButtons.Right:
						NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_RIGHTDOWN, screenPosition.x, screenPosition.y, 0, 0);
						Wait(upDelaySeconds);
						NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_RIGHTUP, screenPosition.x, screenPosition.y, 0, 0);
						break;
					case MouseButtons.Middle:
						NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_MIDDLEDOWN, screenPosition.x, screenPosition.y, 0, 0);
						Wait(upDelaySeconds);
						NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_MIDDLEUP, screenPosition.x, screenPosition.y, 0, 0);
						break;
					default:
						break;
				}

				return true;
			}

			return false;
		}

		public bool DoubleClickImage(string imageName, int xOffset = 0, int yOffset = 0, ClickOrigin origin = ClickOrigin.Center, SearchRegion searchRegion = null)
		{
			throw new NotImplementedException();
		}

		public bool DragDropImage(ImageBuffer imageNeedleDrag, ImageBuffer imageNeedleDrop, int xOffsetDrag = 0, int yOffsetDrag = 0, ClickOrigin originDrag = ClickOrigin.Center,
			int xOffsetDrop = 0, int yOffsetDrop = 0, ClickOrigin originDrop = ClickOrigin.Center,
			SearchRegion searchRegion = null)
		{
			if (searchRegion == null)
			{
				searchRegion = GetScreenRegion();
			}

			if (DragImage(imageNeedleDrag, xOffsetDrag, yOffsetDrag, originDrag, searchRegion))
			{
				return DropImage(imageNeedleDrop, xOffsetDrop, yOffsetDrop, originDrop, searchRegion);
			}

			return false;
		}

		public bool DragDropImage(string imageNameDrag, string imageNameDrop, int xOffsetDrag = 0, int yOffsetDrag = 0, ClickOrigin originDrag = ClickOrigin.Center,
			int xOffsetDrop = 0, int yOffsetDrop = 0, ClickOrigin originDrop = ClickOrigin.Center,
			SearchRegion searchRegion = null)
		{
			ImageBuffer imageNeedleDrag = LoadImageFromSourcFolder(imageNameDrag);
			if (imageNeedleDrag != null)
			{
				ImageBuffer imageNeedleDrop = LoadImageFromSourcFolder(imageNameDrop);
				if (imageNeedleDrop != null)
				{
					return DragDropImage(imageNeedleDrag, imageNeedleDrop, xOffsetDrag, yOffsetDrag, originDrag, xOffsetDrop, yOffsetDrop, originDrop, searchRegion);
				}
			}

			return false;
		}

		public bool DragImage(string imageName, int xOffset = 0, int yOffset = 0, ClickOrigin origin = ClickOrigin.Center, SearchRegion searchRegion = null)
		{
			ImageBuffer imageToLookFor = LoadImageFromSourcFolder(imageName);
			if (imageToLookFor != null)
			{
				return DragImage(imageToLookFor, xOffset, yOffset, origin, searchRegion);
			}

			return false;
		}

		public bool DragImage(ImageBuffer imageNeedle, int xOffset = 0, int yOffset = 0, ClickOrigin origin = ClickOrigin.Center, SearchRegion searchRegion = null)
		{
			if (origin == ClickOrigin.Center)
			{
				xOffset += imageNeedle.Width / 2;
				yOffset += imageNeedle.Height / 2;
			}

			if (searchRegion == null)
			{
				searchRegion = GetScreenRegion();
			}

			Vector2 matchPosition;
			double bestMatch;
			if (searchRegion.Image.FindLeastSquaresMatch(imageNeedle, out matchPosition, out bestMatch, MatchLimit))
			{
				int screenHeight = NativeMethods.GetCurrentScreenHeight();
				int clickY = (int)(searchRegion.ScreenRect.Bottom + matchPosition.y + yOffset);
				int clickYOnScreen = screenHeight - clickY; // invert to put it on the screen

				Point2D screenPosition = new Point2D((int)matchPosition.x + xOffset, clickYOnScreen);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 0, 0);

				return true;
			}

			return false;
		}

		public bool DropImage(string imageName, int xOffset = 0, int yOffset = 0, ClickOrigin origin = ClickOrigin.Center, SearchRegion searchRegion = null)
		{
			ImageBuffer imageToLookFor = LoadImageFromSourcFolder(imageName);
			if (imageToLookFor != null)
			{
				return DropImage(imageToLookFor, xOffset, yOffset, origin, searchRegion);
			}

			return false;
		}

		public bool DropImage(ImageBuffer imageNeedle, int xOffset = 0, int yOffset = 0, ClickOrigin origin = ClickOrigin.Center, SearchRegion searchRegion = null)
		{
			if (origin == ClickOrigin.Center)
			{
				xOffset += imageNeedle.Width / 2;
				yOffset += imageNeedle.Height / 2;
			}

			if (searchRegion == null)
			{
				searchRegion = GetScreenRegion();
			}

			Vector2 matchPosition;
			double bestMatch;
			if (searchRegion.Image.FindLeastSquaresMatch(imageNeedle, out matchPosition, out bestMatch, MatchLimit))
			{
				int screenHeight = NativeMethods.GetCurrentScreenHeight();
				int clickY = (int)(searchRegion.ScreenRect.Bottom + matchPosition.y + yOffset);
				int clickYOnScreen = screenHeight - clickY; // invert to put it on the screen

				Point2D screenPosition = new Point2D((int)matchPosition.x + xOffset, clickYOnScreen);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, screenPosition.x, screenPosition.y, 0, 0);

				return true;
			}

			return false;
		}

		public bool ImageExists(string imageName, SearchRegion searchRegion = null)
		{
			ImageBuffer imageToLookFor = LoadImageFromSourcFolder(imageName);
			if (imageToLookFor != null)
			{
				return ImageExists(imageToLookFor, searchRegion);
			}

			return false;
		}

		public bool ImageExists(ImageBuffer imageNeedle, SearchRegion searchRegion = null)
		{
			if (searchRegion == null)
			{
				searchRegion = GetScreenRegion();
			}

			Vector2 matchPosition;
			double bestMatch;
			if (searchRegion.Image.FindLeastSquaresMatch(imageNeedle, out matchPosition, out bestMatch, MatchLimit))
			{
				return true;
			}

			return false;
		}

		public bool MoveToImage(string imageName, int xOffset = 0, int yOffset = 0, ClickOrigin origin = ClickOrigin.Center, SearchRegion searchRegion = null)
		{
			throw new NotImplementedException();
		}

		private static Point2D GetScreenPosition(double xInWindow, double yInWindow, SystemWindow containingWindow)
		{
			Point2D screenPosition = new Point2D((int)xInWindow, (int)containingWindow.Height - (int)yInWindow);

			screenPosition.x += WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.Location.X;
			screenPosition.y += WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.Location.Y + WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.TitleBarHeight;
			return screenPosition;
		}

		private SearchRegion GetScreenRegion()
		{
			ImageBuffer screenImage = NativeMethods.GetCurrentScreen();
			return new SearchRegion(screenImage, new RectangleInt(0, 0, screenImage.Width, screenImage.Height));
		}

		private ImageBuffer LoadImageFromSourcFolder(string imageName)
		{
			string pathToImage = Path.Combine(imageDirectory, imageName);

			if (File.Exists(pathToImage))
			{
				ImageBuffer imageToLookFor = new ImageBuffer();

				if (ImageIO.LoadImageData(pathToImage, imageToLookFor))
				{
					return imageToLookFor;
				}
			}

			return null;
		}

		#endregion Search By Image

		#region Search By Names

		GuiWidget GetWidgetByName(string widgetName, out SystemWindow containingWindow)
		{
			containingWindow = null;
			foreach (SystemWindow window in SystemWindow.OpenWindows)
			{
				containingWindow = window;
				return window.FindNamedChildRecursive(widgetName);
			}

			return null;
		}

		/// <summary>
		/// Look for a widget with the given name and click it. It and all its parents must be visible and enabled.
		/// </summary>
		/// <param name="widgetName"></param>
		/// <param name="xOffset"></param>
		/// <param name="yOffset"></param>
		/// <param name="origin"></param>
		/// <param name="secondsToWait">Total seconds to stay in this function waiting for the named widget to become visible.</param>
		/// <returns></returns>
		public bool ClickByName(string widgetName, int xOffset = 0, int yOffset = 0, ClickOrigin origin = ClickOrigin.Center, double secondsToWait = 0)
		{
			if (secondsToWait > 0)
			{
				bool foundWidget = WaitForName(widgetName, secondsToWait);
				if (!foundWidget)
				{
					return false;
				}
			}

			SystemWindow containingWindow;
			GuiWidget widgetToClick = GetWidgetByName(widgetName, out containingWindow);
			if (widgetToClick != null)
			{
				RectangleDouble childBounds = widgetToClick.TransformToParentSpace(containingWindow, widgetToClick.LocalBounds);

				if (origin == ClickOrigin.Center)
				{
					xOffset += (int)childBounds.Width / 2;
					yOffset += (int)childBounds.Height / 2;
				}

				Point2D screenPosition = GetScreenPosition(childBounds.Left + xOffset, childBounds.Bottom + yOffset, containingWindow);

				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 0, 0);

				Wait(upDelaySeconds);

				NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, screenPosition.x, screenPosition.y, 0, 0);

				return true;
			}

			return false;
		}

		public bool DragDropByName(string widgetNameDrag, string widgetNameDrop, int xOffsetDrag = 0, int yOffsetDrag = 0, ClickOrigin originDrag = ClickOrigin.Center,
			int xOffsetDrop = 0, int yOffsetDrop = 0, ClickOrigin originDrop = ClickOrigin.Center)
		{
			if (DragByName(widgetNameDrag, xOffsetDrag, yOffsetDrag, originDrag))
			{
				return DropByName(widgetNameDrop, xOffsetDrop, yOffsetDrop, originDrop);
			}

			return false;
		}

		public bool DragByName(string widgetName, int xOffset = 0, int yOffset = 0, ClickOrigin origin = ClickOrigin.Center)
		{
			SystemWindow containingWindow;
			GuiWidget widgetToClick = GetWidgetByName(widgetName, out containingWindow);
			if (widgetToClick != null)
			{
				RectangleDouble childBounds = widgetToClick.TransformToParentSpace(containingWindow, widgetToClick.LocalBounds);

				if (origin == ClickOrigin.Center)
				{
					xOffset += (int)childBounds.Width / 2;
					yOffset += (int)childBounds.Height / 2;
				}

				Point2D screenPosition = GetScreenPosition(childBounds.Left + xOffset, childBounds.Bottom + yOffset, containingWindow);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTDOWN, screenPosition.x, screenPosition.y, 0, 0);

				return true;
			}

			return false;
		}

		public bool DropByName(string widgetName, int xOffset = 0, int yOffset = 0, ClickOrigin origin = ClickOrigin.Center)
		{
			SystemWindow containingWindow;
			GuiWidget widgetToClick = GetWidgetByName(widgetName, out containingWindow);
			if (widgetToClick != null)
			{
				RectangleDouble childBounds = widgetToClick.TransformToParentSpace(containingWindow, widgetToClick.LocalBounds);

				if (origin == ClickOrigin.Center)
				{
					xOffset += (int)childBounds.Width / 2;
					yOffset += (int)childBounds.Height / 2;
				}

				Point2D screenPosition = GetScreenPosition(childBounds.Left + xOffset, childBounds.Bottom + yOffset, containingWindow);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);
				NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_LEFTUP, screenPosition.x, screenPosition.y, 0, 0);

				return true;
			}

			return false;
		}

		public bool DoubleClickByName(string widgetName, int xOffset = 0, int yOffset = 0, ClickOrigin origin = ClickOrigin.Center)
		{
			throw new NotImplementedException();
		}

		public bool MoveToByName(string widgetName, int xOffset = 0, int yOffset = 0, ClickOrigin origin = ClickOrigin.Center)
		{
			SystemWindow containingWindow;
			GuiWidget widgetToClick = GetWidgetByName(widgetName, out containingWindow);
			if (widgetToClick != null)
			{
				RectangleDouble childBounds = widgetToClick.TransformToParentSpace(containingWindow, widgetToClick.LocalBounds);

				if (origin == ClickOrigin.Center)
				{
					xOffset += (int)childBounds.Width / 2;
					yOffset += (int)childBounds.Height / 2;
				}

				Point2D screenPosition = GetScreenPosition(childBounds.Left + xOffset, childBounds.Bottom + yOffset, containingWindow);
				SetMouseCursorPosition(screenPosition.x, screenPosition.y);

				return true;
			}

			return false;
		}

		public bool NameExists(string widgetName)
		{
			foreach (SystemWindow window in SystemWindow.OpenWindows)
			{
				GuiWidget widgetToClick = window.FindNamedChildRecursive(widgetName);
				if (widgetToClick != null)
				{
					if (widgetToClick.AllParentsVisibleAndEnabled())
					{
						return true;
					}
				}
			}

			return false;
		}

		#endregion Search By Names

		public void SetMouseCursorPosition(int x, int y)
		{
			Vector2 start = new Vector2(CurrentMousPosition().x, CurrentMousPosition().y);
			Vector2 end = new Vector2(x, y);
			Vector2 delta = end - start;
			int steps = (int)((TimeToMoveMouse * 1000) / 20);
			for (int i = 0; i < steps; i++)
			{
				double ratio = i / (double)steps;
				ratio = GetInterpolatedValue(ratio, InterpolationType.EASE_OUT);
				Vector2 current = start + delta * ratio;
				NativeMethods.SetCursorPos((int)current.x, (int)current.y);
				Thread.Sleep(20);
			}

			NativeMethods.SetCursorPos((int)end.x, (int)end.y);
		}
		#endregion Mouse Functions

		#region Keyboard Functions

		public void KeyDown(KeyEventArgs keyEvent)
		{
			throw new NotImplementedException();
		}

		public void KeyUp(KeyEventArgs keyEvent)
		{
			throw new NotImplementedException();
		}

		public void Type(string textToType)
		{
			throw new NotImplementedException();
		}

		#endregion Keyboard Functions

		#region Time
		public void Wait(double secondsToWait)
		{
			Thread.Sleep((int)(secondsToWait * 1000));
		}
		public void WaitForImage(string imageName, double secondsToWait, SearchRegion searchRegion = null)
		{
			throw new NotImplementedException();
		}
		public void WaitForImage(ImageBuffer imageNeedle, double secondsToWait, SearchRegion searchRegion = null)
		{
			throw new NotImplementedException();
		}
		/// <summary>
		/// Wait up to secondsToWait for the named widget to exist and be visible.
		/// </summary>
		/// <param name="widgetName"></param>
		public bool WaitForName(string widgetName, double secondsToWait)
		{
			Stopwatch timeWaited = Stopwatch.StartNew();
			while (!NameExists(widgetName)
				&& timeWaited.Elapsed.TotalSeconds < secondsToWait)
			{
				Wait(.05);
			}

			if (timeWaited.Elapsed.TotalSeconds > secondsToWait)
			{
				return false;
			}

			return true;
		}
		#endregion Time
	}
}