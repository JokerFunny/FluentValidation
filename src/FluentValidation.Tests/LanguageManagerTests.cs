﻿namespace FluentValidation.Tests {
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Reflection;
	using Resources;
	using Validators;
	using Xunit;

	public class LanguageManagerTests {
		private ILanguageManager _languages;

		public LanguageManagerTests() {
			_languages = new LanguageManager();
		}

		[Fact]
		public void Gets_translation_for_culture() {
			using (new CultureScope("fr")) {
				var msg = _languages.GetStringForValidator<NotNullValidator>();
				msg.ShouldEqual("'{PropertyName}' ne doit pas avoir la valeur null.");
			}
		}

		[Fact]
		public void Gets_translation_for_specific_culture() {
			using (new CultureScope("zh-CN")) {
				var msg = _languages.GetStringForValidator<NotNullValidator>();
				msg.ShouldEqual("'{PropertyName}' 不能为Null。");
			}
		}

		[Fact]
		public void Gets_translation_for_croatian_culture()
		{
			using (new CultureScope("hr-HR"))
			{
				var msg = _languages.GetStringForValidator<NotNullValidator>();
				msg.ShouldEqual("Niste upisali '{PropertyName}'");
			}
		}

		[Fact]
		public void Falls_back_to_parent_culture() {
			using (new CultureScope("fr-FR")) {
				var msg = _languages.GetStringForValidator<NotNullValidator>();
				msg.ShouldEqual("'{PropertyName}' ne doit pas avoir la valeur null.");
			}
		}

		[Fact]
		public void Falls_back_to_english_when_culture_not_registered() {
			using (new CultureScope("gu-IN")) {
				var msg = _languages.GetStringForValidator<NotNullValidator>();
				msg.ShouldEqual("'{PropertyName}' must not be empty.");
			}
		}

		[Fact]
		public void Falls_back_to_english_when_translation_missing() {
			var l = new LanguageManager();
			l.AddTranslation("en", "TestValidator", "foo");

			using (new CultureScope("zh-CN")) {
				var msg = l.GetStringForValidator<TestValidator>();
				msg.ShouldEqual("foo");
			}
		}

		[Fact]
		public void Always_use_specific_language() {
			_languages.Culture = new CultureInfo("fr-FR");
			var msg = _languages.GetStringForValidator<NotNullValidator>();
			msg.ShouldEqual("'{PropertyName}' ne doit pas avoir la valeur null.");
		}

		[Fact]
		public void Always_use_specific_language_with_string_source() {
			ValidatorOptions.LanguageManager.Culture = new CultureInfo("fr-FR");
			var stringSource = new LanguageStringSource(nameof(NotNullValidator));
			var msg = stringSource.GetString(null);
			ValidatorOptions.LanguageManager.Culture = null;

			msg.ShouldEqual("'{PropertyName}' ne doit pas avoir la valeur null.");
		}

		[Fact]
		public void Disables_localization() {
			using (new CultureScope("fr")) {
				_languages.Enabled = false;
				var msg = _languages.GetStringForValidator<NotNullValidator>();
				msg.ShouldEqual("'{PropertyName}' must not be empty.");
			}
		}

		[Fact]
		public void Can_replace_message() {
			using (new CultureScope("en-US")) {

				var custom = new CustomLanguageManager();
				var msg = custom.GetStringForValidator<NotNullValidator>();
				msg.ShouldEqual("foo");
			}
		}

		[Fact]
		public void Can_replace_message_without_overriding_all_languages() {
			using (new CultureScope("fr-FR")) {

				var custom = new LanguageManager();
				custom.AddTranslation("fr", "NotNullValidator", "foo");
				var msg = custom.GetStringForValidator<NotNullValidator>();
				msg.ShouldEqual("foo");

				// Using a custom translation should only override the single message.
				// Other messages in the language should be unaffected.
				// Need to do this test as non-english, as english is always loaded.
				msg = custom.GetStringForValidator<NotEmptyValidator>();
				msg.ShouldEqual("'{PropertyName}' ne doit pas être vide.");
			}
		}

		[Fact]
		public void All_localizations_have_same_parameters_as_English() {
			// We don't expose the language instances publicly as they're an implementation detail, so have to do a bit of
			// reflection hackery to check all translations across all languages.

			// Get all language instances.
			var languages = typeof(LanguageManager).Assembly.GetTypes()
				.Where(t => typeof(Language).IsAssignableFrom(t) && !t.IsAbstract && t.Name != "GenericLanguage")
				.Select(t => (Language) Activator.CreateInstance(t)).ToList();

			var languageNames = languages.Select(l => l.Name);

			var english = languages.Single(x => x.Name == "en");

			// Get the underlying dictionary.
			var translations = (Dictionary<string, string>) typeof(Language).GetField("_translations", BindingFlags.Instance | BindingFlags.NonPublic)
				.GetValue(english);
			var keys = translations.Keys;

			Assert.All(languageNames, l => Assert.All(keys, k => CheckParametersMatch(l, k)));
		}

		[Fact]
		public void All_languages_should_be_loaded() {
			var languages = typeof(LanguageManager).Assembly.GetTypes()
				.Where(t => typeof(Language).IsAssignableFrom(t) && !t.IsAbstract && t.Name != "GenericLanguage")
				.Select(t => (Language) Activator.CreateInstance(t));

			string englishMessage = _languages.GetString(nameof(NotNullValidator), new CultureInfo("en"));

			foreach (var language in languages) {
				// Skip english as we know it's always loaded and will match.
				if (language.Name == "en") {
					continue;
				}

				// Get the message from the language manager from the culture. If it's in English, then it's hit the
				// fallback and means the culture hasn't been loaded.
				string message = _languages.GetString(nameof(NotNullValidator), new CultureInfo(language.Name));
				(message != englishMessage).ShouldBeTrue($"Language '{language.Name}' ({language.GetType().Name}) is not loaded in the LanguageManager");
			}
		}

		[Fact]
		public void Uses_error_code_as_localization_key() {
			var originalLanguageManager = ValidatorOptions.LanguageManager;
			ValidatorOptions.LanguageManager = new CustomLanguageManager();

			var validator = new InlineValidator<Person>();
			validator.RuleFor(x => x.Forename).NotNull().WithErrorCode("CustomKey");
			var result = validator.Validate(new Person());

			ValidatorOptions.LanguageManager = originalLanguageManager;

			result.Errors[0].ErrorMessage.ShouldEqual("bar");
		}

		[Fact]
		public void Falls_back_to_default_localization_key_when_error_code_key_not_found() {
			var originalLanguageManager = ValidatorOptions.LanguageManager;
			ValidatorOptions.LanguageManager = new CustomLanguageManager();

			var validator = new InlineValidator<Person>();
			validator.RuleFor(x => x.Forename).NotNull().WithErrorCode("DoesNotExist");
			var result = validator.Validate(new Person());

			ValidatorOptions.LanguageManager = originalLanguageManager;

			result.Errors[0].ErrorMessage.ShouldEqual("foo");
		}

		void CheckParametersMatch(string languageCode, string translationKey) {
			var referenceMessage = _languages.GetString(translationKey);
			var translatedMessage = _languages.GetString(translationKey, new CultureInfo(languageCode));
			if (referenceMessage == translatedMessage) return;
			var referenceParameters = ExtractTemplateParameters(referenceMessage);
			var translatedParameters = ExtractTemplateParameters(translatedMessage);
			Assert.False(referenceParameters.Count() != translatedParameters.Count() ||
				referenceParameters.Except(translatedParameters).Any(),
				$"Translation for language {languageCode}, key {translationKey} has parameters {string.Join(",", translatedParameters)}, expected {string.Join(",", referenceParameters)}");
		}

		IEnumerable<string> ExtractTemplateParameters(string message) {
			message = message.Replace("{{", "").Replace("}}", "");
			return message.Split('{').Skip(1).Select(s => s.Split('}').First());
		}

		public class CustomLanguageManager : LanguageManager {
			public CustomLanguageManager() {
				AddTranslation("en", "NotNullValidator", "foo");
				AddTranslation("en", "CustomKey", "bar");
			}
		}

		private class TestValidator : PropertyValidator {
			public TestValidator(IStringSource errorMessageSource) : base(errorMessageSource) {
			}

			public TestValidator(string errorMessage) : base(errorMessage) {
			}

			protected override bool IsValid(PropertyValidatorContext context) {
				return true;
			}
		}
	}
}
