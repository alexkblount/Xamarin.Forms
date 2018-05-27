﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Xamarin.Forms
{
	public interface IShellItemController : IElementController
	{
		void UpdateChecked();

		Task GoToPart(List<string> parts, Dictionary<string, string> queryData);
	}
}