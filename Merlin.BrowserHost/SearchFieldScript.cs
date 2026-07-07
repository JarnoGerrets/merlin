using System.Text.Json;

namespace Merlin.BrowserHost;

internal static class SearchFieldScript
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Create(string query, string? preferredElementId)
    {
        var queryJson = JsonSerializer.Serialize(query, SerializerOptions);
        var preferredElementIdJson = JsonSerializer.Serialize(preferredElementId, SerializerOptions);
        return $$"""
(() => {
  const query = {{queryJson}};
  const preferredElementId = {{preferredElementIdJson}};
  const lower = (value) => String(value || '').toLowerCase();
  const clean = (value) => String(value || '').replace(/\s+/g, ' ').trim();
  const isVisible = (element) => {
    if (!element || !(element instanceof Element)) return false;
    const style = window.getComputedStyle(element);
    if (style.display === 'none' || style.visibility === 'hidden' || Number(style.opacity) === 0) return false;
    const rect = element.getBoundingClientRect();
    return rect.width > 3 && rect.height > 3 && rect.bottom >= 0 && rect.right >= 0 && rect.top <= window.innerHeight && rect.left <= window.innerWidth;
  };
  const isSensitive = (element) => {
    const text = `${lower(element.getAttribute('type'))} ${lower(element.getAttribute('name'))} ${lower(element.id)} ${lower(element.getAttribute('autocomplete'))} ${lower(element.getAttribute('aria-label'))} ${lower(element.getAttribute('placeholder'))}`;
    return /password|passcode|credit|card|cvv|cvc|security|2fa|otp|one-time|verification|login|signin|checkout|payment/.test(text);
  };
  const isUsableInput = (element) => {
    if (!isVisible(element)) return false;
    if (element.disabled || element.readOnly || element.getAttribute('aria-disabled') === 'true') return false;
    if (element.tagName === 'INPUT') {
      const type = lower(element.getAttribute('type') || 'text');
      if (['hidden', 'password', 'checkbox', 'radio', 'file', 'image', 'range', 'color'].includes(type)) return false;
    }
    return !isSensitive(element);
  };
  const scoreSearchField = (element) => {
    const type = lower(element.getAttribute('type'));
    const role = lower(element.getAttribute('role'));
    const name = lower(element.getAttribute('name'));
    const id = lower(element.id);
    const placeholder = lower(element.getAttribute('placeholder'));
    const aria = lower(element.getAttribute('aria-label'));
    const text = `${type} ${role} ${name} ${id} ${placeholder} ${aria}`;
    const rect = element.getBoundingClientRect();
    let score = 0;
    if (isVisible(element)) score += 20;
    if (!element.disabled && !element.readOnly) score += 15;
    if (rect.width >= 120 && rect.height >= 18) score += 10;
    if (rect.top < window.innerHeight * 0.45) score += 8;
    if (element.closest('form[role="search"], form[action*="search"], form[action*="zoek"]')) score += 24;
    if (type === 'search') score += 28;
    if (role === 'searchbox') score += 28;
    if (/\bq\b|search|zoeken|zoek|zoekterm/.test(text)) score += 30;
    return score;
  };
  const candidates = Array.from(document.querySelectorAll('input, textarea, [contenteditable="true"], [role="textbox"], [role="searchbox"]'))
    .filter(isUsableInput)
    .map((element) => ({ element, score: scoreSearchField(element) }))
    .filter((candidate) => candidate.score >= 35)
    .sort((a, b) => b.score - a.score);

  candidates.forEach((candidate, index) => candidate.elementId = `search_${index + 1}`);
  const selected = (preferredElementId ? candidates.find((candidate) => candidate.elementId === preferredElementId) : null) || candidates[0];
  if (!selected) {
    return { success: false, errorCode: 'search_field_not_found', message: 'No search field found.' };
  }

  const element = selected.element;
  const setNativeValue = (target, value) => {
    if (target.isContentEditable) {
      target.focus();
      target.textContent = value;
      target.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: value }));
      target.dispatchEvent(new Event('change', { bubbles: true }));
      return;
    }

    target.focus();
    const valueSetter = Object.getOwnPropertyDescriptor(target, 'value')?.set;
    const prototype = Object.getPrototypeOf(target);
    const prototypeValueSetter = Object.getOwnPropertyDescriptor(prototype, 'value')?.set;
    if (prototypeValueSetter && valueSetter !== prototypeValueSetter) {
      prototypeValueSetter.call(target, value);
    } else if (valueSetter) {
      valueSetter.call(target, value);
    } else {
      target.value = value;
    }
    target.dispatchEvent(new Event('input', { bubbles: true }));
    target.dispatchEvent(new Event('change', { bubbles: true }));
  };
  const clickSearchButton = () => {
    const form = element.closest('form');
    const root = form || document;
    const buttons = Array.from(root.querySelectorAll('button, input[type="submit"], [role="button"]')).filter(isVisible);
    const searchButton = buttons.find((button) => {
      const text = lower(button.innerText || button.textContent || button.getAttribute('aria-label') || button.getAttribute('title') || button.getAttribute('value'));
      return /search|zoeken|zoek|go|submit/.test(text) || lower(button.getAttribute('type')) === 'submit';
    });
    if (!searchButton) return false;
    searchButton.click();
    return true;
  };
  const dispatchEnter = () => {
    const keyOptions = { key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true, cancelable: true };
    element.dispatchEvent(new KeyboardEvent('keydown', keyOptions));
    element.dispatchEvent(new KeyboardEvent('keypress', keyOptions));
    element.dispatchEvent(new KeyboardEvent('keyup', keyOptions));
  };

  setNativeValue(element, query);

  const form = element.closest('form');
  if (form && typeof form.requestSubmit === 'function') {
    form.requestSubmit();
    return { success: true, elementId: selected.elementId, message: 'Search submitted.' };
  }
  if (form) {
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    if (typeof form.submit === 'function') {
      form.submit();
      return { success: true, elementId: selected.elementId, message: 'Search submitted.' };
    }
  }
  if (clickSearchButton()) {
    return { success: true, elementId: selected.elementId, message: 'Search submitted.' };
  }
  dispatchEnter();
  return { success: true, elementId: selected.elementId, message: 'Search submitted.' };
})()
""";
    }
}
