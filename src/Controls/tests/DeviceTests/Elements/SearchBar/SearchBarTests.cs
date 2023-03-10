﻿using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.DeviceTests.Stubs;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform;
using Xunit;

namespace Microsoft.Maui.DeviceTests
{
	[Category(TestCategory.SearchBar)]
	public partial class SearchBarTests : ControlsHandlerTestBase
	{
		void SetupBuilder()
		{
			EnsureHandlerCreated(builder =>
			{
				builder.ConfigureMauiHandlers(handlers =>
				{
					handlers.AddHandler(typeof(Toolbar), typeof(ToolbarHandler));
					handlers.AddHandler(typeof(SearchBar), typeof(SearchBarHandler));
					handlers.AddHandler(typeof(NavigationPage), typeof(NavigationViewHandler));
					handlers.AddHandler<Page, PageHandler>();
					handlers.AddHandler<Window, WindowHandlerStub>();
				});
			});
		}

		[Theory]
		[ClassData(typeof(TextTransformCases))]
		public async Task InitialTextTransformApplied(string text, TextTransform transform, string expected)
		{
			var control = new SearchBar() { Text = text, TextTransform = transform };
			var platformText = await GetPlatformText(await CreateHandlerAsync<SearchBarHandler>(control));
			Assert.Equal(expected, platformText);
		}

		[Theory]
		[ClassData(typeof(TextTransformCases))]
		public async Task TextTransformUpdated(string text, TextTransform transform, string expected)
		{
			var control = new SearchBar() { Text = text };
			var handler = await CreateHandlerAsync<SearchBarHandler>(control);
			await InvokeOnMainThreadAsync(() => control.TextTransform = transform);
			var platformText = await GetPlatformText(handler);
			Assert.Equal(expected, platformText);
		}

#if WINDOWS
		// Only Windows needs the IsReadOnly workaround for MaxLength==0 to prevent text from being entered
		[Fact]
		public async Task MaxLengthIsReadOnlyValueTest()
		{
			SearchBar searchBar = new SearchBar();

			await InvokeOnMainThreadAsync(() =>
			{
				var handler = CreateHandler<SearchBarHandler>(searchBar);
				var platformControl = GetPlatformControl(handler);

				searchBar.MaxLength = 0;
				Assert.True(MauiAutoSuggestBox.GetIsReadOnly(platformControl));
				searchBar.IsReadOnly = false;
				Assert.True(MauiAutoSuggestBox.GetIsReadOnly(platformControl));

				searchBar.MaxLength = 10;
				Assert.False(MauiAutoSuggestBox.GetIsReadOnly(platformControl));
				searchBar.IsReadOnly = true;
				Assert.True(MauiAutoSuggestBox.GetIsReadOnly(platformControl));
			});
		}
#endif

#if ANDROID
		[Fact]
		public async Task SearchBarTakesFullWidth()
		{
			SetupBuilder();

			SearchBar searchBar = new();

			ContentPage page = new()
			{
				Content = searchBar
			};

			NavigationPage navPage = new(new ContentPage());

			await CreateHandlerAndAddToWindow<IWindowHandler>(navPage,
				async (_) =>
				{
					await navPage.CurrentPage.Navigation.PushAsync(page);
				});

			Assert.NotEqual(-1, page.Width);
			Assert.NotEqual(-1, searchBar.Width);
			Assert.Equal(page.Width, searchBar.Width);
		}
#endif
	}
}
