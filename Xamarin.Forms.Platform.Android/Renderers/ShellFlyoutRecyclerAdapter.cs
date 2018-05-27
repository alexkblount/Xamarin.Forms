﻿using Android.Content;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using Xamarin.Forms.Internals;
using AView = Android.Views.View;
using LP = Android.Views.ViewGroup.LayoutParams;

namespace Xamarin.Forms.Platform.Android
{
	public class ShellFlyoutRecyclerAdapter : RecyclerView.Adapter
	{
		private readonly IShellContext _shellContext;

		private DataTemplate _defaultItemTemplate;

		private DataTemplate _defaultMenuItemTemplate;

		private List<AdapterListItem> _listItems;

		private Dictionary<int, DataTemplate> _templateMap = new Dictionary<int, DataTemplate>();
		private readonly Action<Element> _selectedCallback;

		public ShellFlyoutRecyclerAdapter(IShellContext shellContext, Action<Element> selectedCallback)
		{
			_shellContext = shellContext;

			((IShellController)Shell).StructureChanged += OnShellStructureChanged;

			_listItems = GenerateItemList();
			_selectedCallback = selectedCallback;
		}

		public override int ItemCount => _listItems.Count;

		protected Shell Shell => _shellContext.Shell;

		protected virtual DataTemplate DefaultItemTemplate =>
			_defaultItemTemplate ?? (_defaultItemTemplate = new DataTemplate(() => GenerateDefaultCell("Title", "FlyoutIcon")));

		protected virtual DataTemplate DefaultMenuItemTemplate =>
			_defaultMenuItemTemplate ?? (_defaultMenuItemTemplate = new DataTemplate(() => GenerateDefaultCell("Text", "Icon")));

		public override int GetItemViewType(int position)
		{
			var item = _listItems[position];
			var dataTemplate = Shell.ItemTemplate ?? DefaultItemTemplate;
			if (item.Element is MenuItem)
			{
				dataTemplate = Shell.MenuItemTemplate ?? DefaultMenuItemTemplate;
			}

			var template = dataTemplate.SelectDataTemplate(item.Element, Shell);
			var id = ((IDataTemplateController)template).Id;

			_templateMap[id] = template;

			return id;
		}

		public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
		{
			var item = _listItems[position];
			var elementHolder = (ElementViewHolder)holder;

			elementHolder.Bar.Visibility = item.DrawTopLine ? ViewStates.Visible : ViewStates.Gone;
			elementHolder.Element = item.Element;
		}

		public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
		{
			var template = _templateMap[viewType];

			var content = (View)template.CreateContent();

			var linearLayout = new LinearLayout(parent.Context);
			linearLayout.Orientation = Orientation.Vertical;
			linearLayout.LayoutParameters = new RecyclerView.LayoutParams(LP.MatchParent, LP.WrapContent);

			var bar = new AView(parent.Context);
			bar.SetBackgroundColor(Color.Black.MultiplyAlpha(0.2).ToAndroid());
			bar.LayoutParameters = new LP(LP.MatchParent, (int)parent.Context.ToPixels(1));
			linearLayout.AddView(bar);

			var container = new ContainerView(parent.Context, content);
			container.MatchWidth = true;
			container.LayoutParameters = new LP(LP.MatchParent, LP.WrapContent);
			linearLayout.AddView(container);

			return new ElementViewHolder(content, linearLayout, bar, _selectedCallback); ;
		}

		protected virtual List<AdapterListItem> GenerateItemList()
		{
			var result = new List<AdapterListItem>();

			ShellItem previous = null;
			foreach (var item in Shell.Items)
			{
				if (item.FlyoutDisplayOptions == FlyoutDisplayOptions.AsMultipleItems)
				{
					for (int i = 0; i < item.Items.Count; i++)
					{
						var content = item.Items[i];
						result.Add(new AdapterListItem(content, previous != null && i == 0));
					}
				}
				else
				{
					result.Add(new AdapterListItem(item, previous?.FlyoutDisplayOptions == FlyoutDisplayOptions.AsMultipleItems));
				}

				previous = item;
			}

			var menuItems = Shell.MenuItems;
			for (int i = 0; i < menuItems.Count; i++)
			{
				var menuItem = menuItems[i];
				result.Add(new AdapterListItem(menuItem, i == 0));
			}

			return result;
		}

		protected virtual void OnShellStructureChanged(object sender, EventArgs e)
		{
			_listItems = GenerateItemList();
			NotifyDataSetChanged();
		}

		private View GenerateDefaultCell(string textBinding, string iconBinding)
		{
			var grid = new Grid();
			var groups = new VisualStateGroupList();
			
			var commonGroup = new VisualStateGroup();
			commonGroup.Name = "CommonStates";
			groups.Add(commonGroup);

			var normalState = new VisualState();
			normalState.Name = "Normal";
			commonGroup.States.Add(normalState);

			var selectedState = new VisualState();
			selectedState.Name = "Selected";
			selectedState.Setters.Add(new Setter
			{
				Property = VisualElement.BackgroundColorProperty,
				Value = new Color(0.95)
			});

			commonGroup.States.Add(selectedState);

			VisualStateManager.SetVisualStateGroups(grid, groups);

			grid.HeightRequest = 40;
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = 50 });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

			var image = new Image();
			image.VerticalOptions = image.HorizontalOptions = LayoutOptions.Center;
			image.HeightRequest = image.WidthRequest = 22;
			image.SetBinding(Image.SourceProperty, iconBinding);
			grid.Children.Add(image);

			var label = new Label();
			label.VerticalTextAlignment = TextAlignment.Center;
			label.SetBinding(Label.TextProperty, textBinding);
			grid.Children.Add(label, 1, 0);

			label.FontSize = 14;
			label.TextColor = Color.Black;
			label.FontFamily = "sans-serif-medium";

			return grid;
		}

		public class AdapterListItem
		{
			public AdapterListItem(Element element, bool drawTopLine = false)
			{
				DrawTopLine = drawTopLine;
				Element = element;
			}

			public bool DrawTopLine { get; set; }
			public Element Element { get; set; }
		}

		public class ElementViewHolder : RecyclerView.ViewHolder
		{
			private readonly Action<Element> _selectedCallback;
			private Element _element;

			public ElementViewHolder(View view, AView itemView, AView bar, Action<Element> selectedCallback) : base(itemView)
			{
				itemView.Click += OnClicked;
				View = view;
				Bar = bar;
				_selectedCallback = selectedCallback;
			}

			private void OnClicked(object sender, EventArgs e)
			{
				if (Element == null)
					return;

				_selectedCallback(Element);
			}

			public View View { get; }
			public AView Bar { get; }
			public Element Element
			{
				get { return _element; }
				set
				{
					if (_element == value)
						return;

					if (_element != null && _element is BaseShellItem)
						_element.PropertyChanged -= OnElementPropertyChanged;

					_element = value;
					View.BindingContext = value;

					if (_element != null)
					{
						_element.PropertyChanged += OnElementPropertyChanged;
						UpdateVisualState();
					}
				}
			}

			private void UpdateVisualState()
			{
				if (Element is BaseShellItem baseShellItem && baseShellItem != null)
				{
					if (baseShellItem.IsChecked)
						VisualStateManager.GoToState(View, "Selected");
					else
						VisualStateManager.GoToState(View, "Normal");
				}
			}

			private void OnElementPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
			{
				if (e.PropertyName == BaseShellItem.IsCheckedProperty.PropertyName)
				{
					UpdateVisualState();
				}
			}
		}
	}
}