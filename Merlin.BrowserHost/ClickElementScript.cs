using System.Text.Json;

namespace Merlin.BrowserHost;

internal static class ClickElementScript
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Create(string elementId, string? snapshotId, string? expectedText, string? expectedHref)
    {
        var elementIdJson = JsonSerializer.Serialize(elementId, SerializerOptions);
        var snapshotIdJson = JsonSerializer.Serialize(snapshotId, SerializerOptions);
        var expectedTextJson = JsonSerializer.Serialize(expectedText, SerializerOptions);
        var expectedHrefJson = JsonSerializer.Serialize(expectedHref, SerializerOptions);
        return $$"""
(() => {
  const elementId = {{elementIdJson}};
  const snapshotId = {{snapshotIdJson}};
  const expectedText = {{expectedTextJson}};
  const expectedHref = {{expectedHrefJson}};
  const selector = snapshotId
    ? `[data-merlin-snapshot-id="${CSS.escape(snapshotId)}"][data-merlin-element-id="${CSS.escape(elementId)}"]`
    : `[data-merlin-element-id="${CSS.escape(elementId)}"]`;
  const element = document.querySelector(selector);
  const clean = (value) => String(value || '').replace(/\s+/g, ' ').trim().toLowerCase();
  const short = (value) => String(value || '').replace(/\s+/g, ' ').trim().slice(0, 160);
  const textFor = (target) => clean(target?.innerText || target?.textContent || target?.getAttribute?.('aria-label') || target?.getAttribute?.('title') || target?.getAttribute?.('value'));
  const hrefFor = (target) => target?.href || target?.getAttribute?.('href') || '';
  const parseUrl = (value) => {
    try {
      return value ? new URL(value, document.baseURI) : null;
    } catch {
      return null;
    }
  };
  const youtubeVideoId = (value) => {
    const url = parseUrl(value);
    if (!url || !url.hostname.includes('youtube.com') || url.pathname !== '/watch') return null;
    return url.searchParams.get('v');
  };
  const hrefMatches = (expected, current) => {
    const expectedVideo = youtubeVideoId(expected);
    const currentVideo = youtubeVideoId(current);
    if (expectedVideo && currentVideo) return expectedVideo === currentVideo;
    return clean(expected) === clean(current);
  };
  const isClickable = (target) => {
    if (!target || !(target instanceof Element)) return false;
    if (target.matches('a[href], button, [role="button"], [role="link"], input[type="submit"], input[type="button"]')) return true;
    return false;
  };
  const isVisible = (target) => {
    if (!target || !(target instanceof Element)) return false;
    const style = window.getComputedStyle(target);
    const rect = target.getBoundingClientRect();
    return !(style.display === 'none' || style.visibility === 'hidden' || Number(style.opacity) === 0 || rect.width <= 2 || rect.height <= 2);
  };
  const isEnabled = (target) => !(target.disabled || target.getAttribute('aria-disabled') === 'true');

  if (!element) {
    return {
      success: false,
      errorCode: 'stale_element',
      message: `Element no longer exists for SnapshotId="${short(snapshotId)}".`,
      elementId
    };
  }

  if (!isVisible(element)) {
    return { success: false, errorCode: 'not_visible', message: 'Element is no longer visible.', elementId };
  }
  if (!isEnabled(element)) {
    return { success: false, errorCode: 'not_enabled', message: 'Element is disabled.', elementId };
  }

  const clickableAncestor = element.closest('a[href], button, [role="button"], [role="link"], input[type="submit"], input[type="button"]');
  const clickableChild = element.querySelector?.('a[href], button, [role="button"], [role="link"], input[type="submit"], input[type="button"]');
  const currentText = textFor(element);
  const currentHref = hrefFor(element) || hrefFor(clickableAncestor) || hrefFor(clickableChild);
  const expectedClean = clean(expectedText);
  const expectedHrefClean = clean(expectedHref);
  const currentHrefClean = clean(currentHref);
  if (expectedHrefClean) {
    if (!currentHrefClean) {
      return {
        success: false,
        errorCode: 'element_mismatch',
        message: `Element href disappeared. ExpectedHref="${short(expectedHref)}" CurrentText="${short(currentText)}"`,
        elementId
      };
    }
    if (!hrefMatches(expectedHref, currentHref)) {
      return {
        success: false,
        errorCode: 'element_mismatch',
        message: `Element href changed. ExpectedHref="${short(expectedHref)}" CurrentHref="${short(currentHref)}" ExpectedText="${short(expectedText)}" CurrentText="${short(currentText)}"`,
        elementId
      };
    }
  }
  else if (expectedClean && currentText && !currentText.includes(expectedClean) && !expectedClean.includes(currentText)) {
    return {
      success: false,
      errorCode: 'element_mismatch',
      message: `Element text changed. ExpectedText="${short(expectedText)}" CurrentText="${short(currentText)}"`,
      elementId
    };
  }

  const clickTarget = isClickable(element)
    ? element
    : (isClickable(clickableAncestor) ? clickableAncestor : (isClickable(clickableChild) ? clickableChild : null));

  if (!clickTarget) {
    return { success: false, errorCode: 'no_clickable_target', message: 'No clickable target found.', elementId };
  }

  if (!isVisible(clickTarget)) {
    return { success: false, errorCode: 'not_visible', message: 'Clickable target is not visible.', elementId };
  }
  if (!isEnabled(clickTarget)) {
    return { success: false, errorCode: 'not_enabled', message: 'Clickable target is disabled.', elementId };
  }

  clickTarget.scrollIntoView({ block: 'center', inline: 'center', behavior: 'smooth' });
  clickTarget.focus?.({ preventScroll: true });

  const dispatchPointerSequence = () => {
    const currentRect = clickTarget.getBoundingClientRect();
    const x = currentRect.left + currentRect.width / 2;
    const y = currentRect.top + currentRect.height / 2;
    const common = { bubbles: true, cancelable: true, clientX: x, clientY: y, view: window };
    for (const type of ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click']) {
      const event = type.startsWith('pointer')
        ? new PointerEvent(type, { ...common, pointerId: 1, pointerType: 'mouse', isPrimary: true })
        : new MouseEvent(type, common);
      clickTarget.dispatchEvent(event);
    }
  };

  try {
    clickTarget.click();
    dispatchPointerSequence();
    return { success: true, elementId, message: 'Clicked.', fallbackUsed: clickTarget !== element };
  } catch (error) {
    return { success: false, errorCode: 'script_exception', message: String(error?.message || error), elementId };
  }
})()
""";
    }
}
