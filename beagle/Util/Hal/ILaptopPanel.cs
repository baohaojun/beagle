//
//  ILaptopPanel.cs
//
//  Copyright (c) 2007 Lukas Lipka, <lukaslipka@gmail.com>.
//

using System;
using System.Collections;
using System.Collections.Generic;

using DBus;

namespace Hal {

	[Interface ("org.freedesktop.Hal.Device.LaptopPanel")]
	internal interface ILaptopPanel {
		int GetBrightness ();
		void SetBrightness (int brightness);
	}
}