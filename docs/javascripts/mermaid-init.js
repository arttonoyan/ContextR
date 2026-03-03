window.mermaidConfig = {
  startOnLoad: false,
  securityLevel: "loose",
  theme: "default"
};

if (typeof mermaid !== "undefined") {
  mermaid.initialize(window.mermaidConfig);
}

const renderMermaid = () => {
  if (typeof mermaid === "undefined") {
    return;
  }

  const mermaidBlocks = document.querySelectorAll("pre code.language-mermaid, div.mermaid");
  if (mermaidBlocks.length === 0) {
    return;
  }

  mermaidBlocks.forEach((block) => {
    if (block.classList.contains("mermaid")) {
      return;
    }

    const container = document.createElement("div");
    container.className = "mermaid";
    container.textContent = block.textContent ?? "";
    const pre = block.closest("pre");
    if (pre && pre.parentNode) {
      pre.parentNode.replaceChild(container, pre);
    }
  });

  mermaid.run({
    querySelector: ".mermaid"
  });
};

document$.subscribe(renderMermaid);
