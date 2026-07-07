using System.Text.Json;

namespace Merlin.BrowserHost;

internal static class CommonActionScript
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Create(string action)
    {
        var actionJson = JsonSerializer.Serialize(action, SerializerOptions);
        return $$"""
(async () => {
  const action = {{actionJson}};
  const clean = (value) => String(value || '').replace(/\s+/g, ' ').trim().toLowerCase();
  const visibleEnough = (element) => {
    if (!element || !(element instanceof Element)) return false;
    const style = window.getComputedStyle(element);
    const rect = element.getBoundingClientRect();
    return style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 2 && rect.height > 2;
  };
  const isEnabled = (element) => !(element.disabled || element.getAttribute('aria-disabled') === 'true');
  const textFor = (element) => clean([
    element?.innerText,
    element?.textContent,
    element?.getAttribute?.('aria-label'),
    element?.getAttribute?.('title'),
    element?.getAttribute?.('data-title-no-tooltip'),
    element?.getAttribute?.('data-tooltip-title'),
    element?.id,
    element?.className
  ].filter(Boolean).join(' '));
  const dispatchPointerSequence = (target) => {
    const rect = target.getBoundingClientRect();
    const x = rect.left + rect.width / 2;
    const y = rect.top + rect.height / 2;
    const common = { bubbles: true, cancelable: true, clientX: x, clientY: y, view: window };
    for (const type of ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click']) {
      const event = type.startsWith('pointer')
        ? new PointerEvent(type, { ...common, pointerId: 1, pointerType: 'mouse', isPrimary: true })
        : new MouseEvent(type, common);
      target.dispatchEvent(event);
    }
  };
  const click = (target, elementId) => {
    if (!target || !visibleEnough(target) || !isEnabled(target)) return false;
    target.scrollIntoView?.({ block: 'center', inline: 'center', behavior: 'instant' });
    target.focus?.({ preventScroll: true });
    target.click();
    dispatchPointerSequence(target);
    return { success: true, elementId, message: 'Clicked.' };
  };
  const revealMediaControls = async () => {
    const target = document.querySelector('.html5-video-player, .ytp-chrome-bottom, video');
    if (!target) return;
    const rect = target.getBoundingClientRect();
    const x = Math.max(rect.left + 10, Math.min(rect.left + rect.width / 2, window.innerWidth - 10));
    const y = Math.max(rect.top + 10, Math.min(rect.top + rect.height / 2, window.innerHeight - 10));
    for (const type of ['mousemove', 'pointermove']) {
      target.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, clientX: x, clientY: y, view: window }));
    }
    await new Promise(resolve => setTimeout(resolve, 120));
  };
  const first = (selectors) => {
    for (const selector of selectors) {
      const element = document.querySelector(selector);
      if (element) return element;
    }
    return null;
  };
  const findByLabels = (labels) => {
    const controls = Array.from(document.querySelectorAll('button, [role="button"], a[href], [role="link"], input[type="button"], input[type="submit"]'));
    return controls.find(element => visibleEnough(element) && isEnabled(element) && labels.some(label => textFor(element).includes(label)));
  };

  await revealMediaControls();

  const video = document.querySelector('video');
  let target = null;
  let elementId = null;

  if (action === 'pause_video') {
    if (video && video.paused) return { success: true, elementId: 'video', message: 'Already paused.' };
    target = first(['.ytp-play-button', 'button[aria-keyshortcuts="k"]']) || findByLabels(['pause', 'pauze', 'pauzeren']);
    elementId = target?.getAttribute('data-merlin-element-id') || target?.id || 'ytp-play-button';
  } else if (action === 'play_video') {
    if (video && !video.paused) return { success: true, elementId: 'video', message: 'Already playing.' };
    target = first(['.ytp-play-button', 'button[aria-keyshortcuts="k"]']) || findByLabels(['play', 'afspelen']);
    elementId = target?.getAttribute('data-merlin-element-id') || target?.id || 'ytp-play-button';
  } else if (action === 'skip_ad') {
    target = first(['.ytp-skip-ad-button', '.ytp-ad-skip-button', '[id^="skip-button"]']) || findByLabels(['skip ad', 'skip', 'overslaan', 'advertentie overslaan']);
    elementId = target?.getAttribute('data-merlin-element-id') || target?.id || 'ytp-skip-ad-button';
  } else if (action === 'mute_video') {
    if (video && video.muted) return { success: true, elementId: 'video', message: 'Already muted.' };
    target = first(['.ytp-mute-button']) || findByLabels(['mute', 'dempen']);
    elementId = target?.getAttribute('data-merlin-element-id') || target?.id || 'ytp-mute-button';
  } else if (action === 'unmute_video') {
    if (video && !video.muted) return { success: true, elementId: 'video', message: 'Already unmuted.' };
    target = first(['.ytp-mute-button']) || findByLabels(['unmute', 'geluid aan', 'dempen opheffen']);
    elementId = target?.getAttribute('data-merlin-element-id') || target?.id || 'ytp-mute-button';
  } else if (action === 'fullscreen' || action === 'exit_fullscreen') {
    target = first(['.ytp-fullscreen-button']) || findByLabels(['fullscreen', 'full screen', 'volledig scherm']);
    elementId = target?.getAttribute('data-merlin-element-id') || target?.id || 'ytp-fullscreen-button';
  }

  const clicked = click(target, elementId);
  if (clicked) return clicked;

  return {
    success: false,
    errorCode: 'common_action_not_found',
    message: `No target found for ${action}.`,
    elementId
  };
})()
""";
    }
}
