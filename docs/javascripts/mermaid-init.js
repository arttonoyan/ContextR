window.mermaidConfig = {
  startOnLoad: false,
  securityLevel: "loose",
  theme: "default"
};

if (typeof mermaid !== "undefined") {
  mermaid.initialize(window.mermaidConfig);
}

const selectMermaidSources = (root) =>
  root.querySelectorAll(
    [
      "div.mermaid",
      "pre code.language-mermaid",
      "pre.mermaid code",
      "code.mermaid"
    ].join(",")
  );

const normalizeToMermaidContainers = (root) => {
  const containers = [];
  const sources = selectMermaidSources(root);

  sources.forEach((source) => {
    if (source instanceof HTMLDivElement && source.classList.contains("mermaid")) {
      containers.push(source);
      return;
    }

    if (!(source instanceof HTMLElement)) {
      return;
    }

    const sourceText = source.textContent ?? "";
    if (!sourceText.trim()) {
      return;
    }

    const container = document.createElement("div");
    container.className = "mermaid";
    container.textContent = sourceText;

    const pre = source.closest("pre");
    if (pre && pre.parentNode) {
      pre.parentNode.replaceChild(container, pre);
      containers.push(container);
      return;
    }

    if (source.parentNode) {
      source.parentNode.replaceChild(container, source);
      containers.push(container);
    }
  });

  return containers;
};

const renderMermaid = async () => {
  if (typeof mermaid === "undefined") {
    return;
  }

  const containers = normalizeToMermaidContainers(document);
  if (containers.length === 0) {
    return;
  }

  let diagramIndex = 0;
  for (const container of containers) {
    if (!(container instanceof HTMLElement)) {
      continue;
    }

    if (container.dataset.mermaidRendered === "true") {
      continue;
    }

    const definition = container.textContent?.trim() ?? "";
    if (!definition) {
      continue;
    }

    const idSuffix = `${Date.now()}-${diagramIndex++}`;
    try {
      const { svg, bindFunctions } = await mermaid.render(`mermaid-${idSuffix}`, definition);
      container.innerHTML = svg;
      container.dataset.mermaidRendered = "true";

      if (typeof bindFunctions === "function") {
        bindFunctions(container);
      }
    } catch (error) {
      console.error("Mermaid render failed:", error);
      container.dataset.mermaidError = "true";
    }
  }
};

document$.subscribe(renderMermaid);
