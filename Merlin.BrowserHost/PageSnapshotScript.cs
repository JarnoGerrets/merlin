using System.Text.Json;

namespace Merlin.BrowserHost;

internal static class PageSnapshotScript
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Create(BrowserPageSnapshotRequestOptions options)
    {
        var json = JsonSerializer.Serialize(options, SerializerOptions);
        return $$"""
(() => {
  const options = {{json}};
  const snapshotId = `snapshot_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 10)}`;
  const maxText = Math.max(40, options.maxElementTextLength || 300);
  const counters = { input: 0, search: 0, button: 0, link: 0, heading: 0, result: 0, textBlock: 0 };
  let totalElementCount = 0;
  let isTruncated = false;

  document
    .querySelectorAll('[data-merlin-element-id], [data-merlin-snapshot-id]')
    .forEach(element => {
      element.removeAttribute('data-merlin-element-id');
      element.removeAttribute('data-merlin-snapshot-id');
    });

  const cap = (name) => Math.max(0, options[name] || 0);
  const trimText = (value, max = maxText) => {
    if (!value) return null;
    const text = String(value).replace(/\s+/g, ' ').trim();
    if (!text) return null;
    return text.length > max ? text.slice(0, max - 3) + '...' : text;
  };
  const lower = (value) => String(value || '').toLowerCase();
  const rectFor = (element) => {
    const rect = element.getBoundingClientRect();
    return { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
  };
  const intersectsViewport = (rect) => (
    rect.width > 1 && rect.height > 1 &&
    rect.bottom >= -200 &&
    rect.right >= -200 &&
    rect.top <= window.innerHeight + 200 &&
    rect.left <= window.innerWidth + 200
  );
  const isVisible = (element) => {
    if (!element || !(element instanceof Element)) return false;
    const style = window.getComputedStyle(element);
    if (style.display === 'none' || style.visibility === 'hidden' || Number(style.opacity) === 0) return false;
    const rect = element.getBoundingClientRect();
    return rect.width > 1 && rect.height > 1 && intersectsViewport(rect);
  };
  const isEnabled = (element) => !element.disabled && element.getAttribute('aria-disabled') !== 'true';
  const nextId = (prefix) => `${prefix}_${++counters[prefix]}`;
  const elementIdFor = (prefix, element) => {
    const existingSnapshotId = element.getAttribute('data-merlin-snapshot-id');
    const existingId = element.getAttribute('data-merlin-element-id');
    if (existingSnapshotId === snapshotId && existingId) return existingId;

    const id = nextId(prefix);
    element.setAttribute('data-merlin-element-id', id);
    element.setAttribute('data-merlin-snapshot-id', snapshotId);
    return id;
  };
  const labelFor = (element) => {
    if (!element) return null;
    if (element.id) {
      const direct = document.querySelector(`label[for="${CSS.escape(element.id)}"]`);
      const text = trimText(direct?.innerText || direct?.textContent);
      if (text) return text;
    }
    return trimText(element.closest('label')?.innerText || element.closest('label')?.textContent);
  };
  const titleOrText = (element) => trimText(
    element.innerText ||
    element.textContent ||
    element.getAttribute('data-title-no-tooltip') ||
    element.getAttribute('data-tooltip-title') ||
    element.getAttribute('aria-label') ||
    element.getAttribute('title') ||
    element.getAttribute('value'));
  const makeElement = (prefix, type, element, extra = {}) => {
    const rect = element.getBoundingClientRect();
    return {
      id: elementIdFor(prefix, element),
      type,
      text: trimText(extra.text ?? (element.innerText || element.textContent)),
      label: trimText(extra.label ?? labelFor(element)),
      ariaLabel: trimText(element.getAttribute('aria-label')),
      title: trimText(element.getAttribute('title')),
      dataTitleNoTooltip: trimText(element.getAttribute('data-title-no-tooltip')),
      dataTooltipTitle: trimText(element.getAttribute('data-tooltip-title')),
      placeholder: trimText(element.getAttribute('placeholder')),
      name: trimText(element.getAttribute('name')),
      domId: trimText(element.id),
      cssClass: trimText(element.getAttribute('class')),
      role: trimText(element.getAttribute('role')),
      href: extra.href ?? null,
      valuePreview: null,
      rect: rectFor(element),
      isVisible: isVisible(element),
      isEnabled: isEnabled(element),
      isInViewport: rect.bottom >= 0 && rect.right >= 0 && rect.top <= window.innerHeight && rect.left <= window.innerWidth,
      score: extra.score ?? 0
    };
  };
  const pushCapped = (array, item, max) => {
    totalElementCount++;
    if (array.length >= max) {
      isTruncated = true;
      return;
    }
    array.push(item);
  };

  const inputs = [];
  const inputElements = Array.from(document.querySelectorAll('input, textarea, [contenteditable="true"], [role="textbox"], [role="searchbox"]'))
    .filter(isVisible);
  for (const element of inputElements) {
    const item = makeElement('input', 'input', element);
    pushCapped(inputs, item, cap('maxInputs'));
  }

  const searchFields = [];
  const scoreSearchField = (element) => {
    const type = lower(element.getAttribute('type'));
    const role = lower(element.getAttribute('role'));
    const name = lower(element.getAttribute('name'));
    const id = lower(element.id);
    const placeholder = lower(element.getAttribute('placeholder'));
    const aria = lower(element.getAttribute('aria-label'));
    const text = `${type} ${role} ${name} ${id} ${placeholder} ${aria}`;
    let score = 0;
    const rect = element.getBoundingClientRect();
    if (isVisible(element)) score += 20;
    if (isEnabled(element)) score += 15;
    if (rect.top >= 0 && rect.top <= window.innerHeight) score += 10;
    if (rect.width >= 120 && rect.height >= 18) score += 8;
    if (rect.top < window.innerHeight * 0.45) score += 8;
    if (element.closest('form[role="search"], form[action*="search"], form[action*="zoek"]')) score += 20;
    if (type === 'search') score += 25;
    if (role === 'searchbox') score += 25;
    if (/\bq\b|search|zoeken|zoek|zoekterm/.test(text)) score += 25;
    return score;
  };
  for (const element of inputElements) {
    const score = scoreSearchField(element);
    if (score < 35) continue;
    const item = makeElement('search', 'searchField', element, { score });
    searchFields.push(item);
  }
  searchFields.sort((a, b) => b.score - a.score);
  if (searchFields.length > cap('maxSearchFields')) isTruncated = true;

  const buttons = [];
  const buttonElements = Array.from(document.querySelectorAll('button, input[type="submit"], input[type="button"], input[type="reset"], [role="button"]'))
    .filter(isVisible);
  for (const element of buttonElements) {
    const item = makeElement('button', 'button', element, { text: titleOrText(element) });
    pushCapped(buttons, item, cap('maxButtons'));
  }

  const links = [];
  const linkElements = Array.from(document.querySelectorAll('a[href], [role="link"]'))
    .filter(isVisible)
    .filter(element => titleOrText(element) || element.getAttribute('aria-label'))
    .filter(element => {
      const rect = element.getBoundingClientRect();
      return rect.width > 3 && rect.height > 3;
    });
  for (const element of linkElements) {
    const href = element.href || element.getAttribute('href');
    if (href && href.startsWith('javascript:') && !titleOrText(element)) continue;
    const item = makeElement('link', 'link', element, { text: titleOrText(element), href });
    pushCapped(links, item, cap('maxLinks'));
  }

  const headings = [];
  const headingElements = Array.from(document.querySelectorAll('h1, h2, h3, [role="heading"]'))
    .filter(isVisible)
    .filter(element => titleOrText(element));
  for (const element of headingElements) {
    const level = element.tagName.match(/^H([1-6])$/i)?.[1] || element.getAttribute('aria-level');
    const item = makeElement('heading', 'heading', element, { text: titleOrText(element), score: Number(level || 0) });
    pushCapped(headings, item, cap('maxHeadings'));
  }

  const results = [];
  const resultCandidates = linkElements
    .map(element => {
      const text = titleOrText(element);
      const container = element.closest('article, li, section, [role="article"], [class*="result"], [class*="card"], [data-testid*="result"]');
      let score = (text?.length || 0) + (container ? 40 : 0);
      const tag = element.closest('h2, h3') ? 25 : 0;
      score += tag;
      return { element, text, score };
    })
    .filter(candidate => candidate.text && candidate.text.length >= 20 && candidate.score >= 45)
    .sort((a, b) => b.score - a.score);
  for (const candidate of resultCandidates) {
    const item = makeElement('result', 'result', candidate.element, {
      text: candidate.text,
      href: candidate.element.href || candidate.element.getAttribute('href'),
      score: candidate.score
    });
    pushCapped(results, item, cap('maxResults'));
  }

  const textBlocks = [];
  const textElements = Array.from(document.querySelectorAll('main p, article p, section p, p, li, main div, article div'))
    .filter(isVisible)
    .map(element => ({ element, text: trimText(element.innerText || element.textContent, 500) }))
    .filter(item => item.text && item.text.length >= 40)
    .filter((item, index, array) => array.findIndex(other => other.text === item.text) === index);
  for (const item of textElements) {
    const snapshotElement = makeElement('textBlock', 'textBlock', item.element, { text: item.text });
    pushCapped(textBlocks, snapshotElement, cap('maxTextBlocks'));
  }

  const limitedSearchFields = searchFields.slice(0, cap('maxSearchFields'));
  return {
    snapshotId,
    url: document.location.href,
    title: document.title,
    capturedAtUtc: new Date().toISOString(),
    inputs,
    searchFields: limitedSearchFields,
    buttons,
    links,
    headings,
    results,
    textBlocks,
    totalElementCount,
    isTruncated,
    error: null
  };
})()
""";
    }
}
