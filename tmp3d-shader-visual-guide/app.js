const state = {
  stage: 0,
  depth: 0.72,
  outline: 0.08,
  raySteps: 28,
  time: 0
};

const stageCopy = [
  {
    caption: "TMPの文字は最初、1文字4頂点の薄い板です。",
    text: "TMPは文字ごとに四角形を作ります。ここでは “TMP” の3文字なので、入力は3クワッド、つまり12頂点です。"
  },
  {
    caption: "Compute shaderが、各クワッドに前面・背面・側面を追加します。",
    text: "1クワッドを16頂点と36インデックスに展開します。UnityのMesh Assetを保存するのではなく、GPUバッファとして描画時に使います。"
  },
  {
    caption: "ピクセルごとに視線のレイを進め、SDFで文字の表面を探します。",
    text: "箱の中を少しずつ進み、3D位置をSDFアトラスのUVへ変換します。SDF値がしきい値を越えた位置が文字の見える面です。"
  },
  {
    caption: "ヒットした位置に色、アウトライン、depth色、疑似ライトを重ねます。",
    text: "最後は通常のTMP色だけでなく、Depth Albedo、Outline、Bevel風の法線、Specularなどを組み合わせて立体感を出します。"
  }
];

const els = {
  main: document.querySelector("#mainStage"),
  sdfIntro: document.querySelector("#sdfIntroStage"),
  quad: document.querySelector("#quadStage"),
  sdf: document.querySelector("#sdfStage"),
  ray: document.querySelector("#rayStage"),
  tradeoff: document.querySelector("#tradeoffStage"),
  depth: document.querySelector("#depthRange"),
  outline: document.querySelector("#outlineRange"),
  raySteps: document.querySelector("#rayRange"),
  depthValue: document.querySelector("#depthValue"),
  outlineValue: document.querySelector("#outlineValue"),
  rayValue: document.querySelector("#rayValue"),
  quadStat: document.querySelector("#quadStat"),
  vertexStat: document.querySelector("#vertexStat"),
  indexStat: document.querySelector("#indexStat"),
  stageCaption: document.querySelector("#stageCaption"),
  stageText: document.querySelector("#stageText")
};

const quads = 3;

function clamp(v, min, max) {
  return Math.max(min, Math.min(max, v));
}

function lerp(a, b, t) {
  return a + (b - a) * t;
}

function setupCanvas(canvas) {
  const rect = canvas.getBoundingClientRect();
  const dpr = Math.max(1, Math.min(window.devicePixelRatio || 1, 2));
  const width = Math.max(320, Math.round(rect.width * dpr));
  const height = Math.max(220, Math.round(rect.height * dpr));
  if (canvas.width !== width || canvas.height !== height) {
    canvas.width = width;
    canvas.height = height;
  }
  const ctx = canvas.getContext("2d");
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  return { ctx, w: width / dpr, h: height / dpr };
}

const imageDataBuffer = {
  canvas: null,
  ctx: null
};

function drawImageDataAt(ctx, imageData, x, y, width = imageData.width, height = imageData.height) {
  if (!imageDataBuffer.canvas) {
    imageDataBuffer.canvas = document.createElement("canvas");
    imageDataBuffer.ctx = imageDataBuffer.canvas.getContext("2d");
  }
  if (imageDataBuffer.canvas.width !== imageData.width || imageDataBuffer.canvas.height !== imageData.height) {
    imageDataBuffer.canvas.width = imageData.width;
    imageDataBuffer.canvas.height = imageData.height;
  }
  imageDataBuffer.ctx.putImageData(imageData, 0, 0);

  ctx.save();
  ctx.imageSmoothingEnabled = true;
  ctx.imageSmoothingQuality = "high";
  ctx.drawImage(imageDataBuffer.canvas, x, y, width, height);
  ctx.restore();
}

function roundedRect(ctx, x, y, w, h, r) {
  const rr = Math.min(r, w / 2, h / 2);
  ctx.beginPath();
  ctx.moveTo(x + rr, y);
  ctx.lineTo(x + w - rr, y);
  ctx.quadraticCurveTo(x + w, y, x + w, y + rr);
  ctx.lineTo(x + w, y + h - rr);
  ctx.quadraticCurveTo(x + w, y + h, x + w - rr, y + h);
  ctx.lineTo(x + rr, y + h);
  ctx.quadraticCurveTo(x, y + h, x, y + h - rr);
  ctx.lineTo(x, y + rr);
  ctx.quadraticCurveTo(x, y, x + rr, y);
  ctx.closePath();
}

function label(ctx, text, x, y, tone = "#202124") {
  ctx.save();
  ctx.font = "700 13px system-ui, sans-serif";
  const width = ctx.measureText(text).width + 18;
  roundedRect(ctx, x, y - 18, width, 28, 6);
  ctx.fillStyle = "rgba(255,255,255,0.9)";
  ctx.fill();
  ctx.strokeStyle = "rgba(32,33,36,0.18)";
  ctx.stroke();
  ctx.fillStyle = tone;
  ctx.fillText(text, x + 9, y);
  ctx.restore();
}

function arrow(ctx, x1, y1, x2, y2, color = "#202124") {
  const angle = Math.atan2(y2 - y1, x2 - x1);
  ctx.save();
  ctx.strokeStyle = color;
  ctx.fillStyle = color;
  ctx.lineWidth = 2.5;
  ctx.beginPath();
  ctx.moveTo(x1, y1);
  ctx.lineTo(x2, y2);
  ctx.stroke();
  ctx.beginPath();
  ctx.moveTo(x2, y2);
  ctx.lineTo(x2 - 12 * Math.cos(angle - 0.45), y2 - 12 * Math.sin(angle - 0.45));
  ctx.lineTo(x2 - 12 * Math.cos(angle + 0.45), y2 - 12 * Math.sin(angle + 0.45));
  ctx.closePath();
  ctx.fill();
  ctx.restore();
}

function drawDot(ctx, x, y, color, r = 4) {
  ctx.beginPath();
  ctx.arc(x, y, r, 0, Math.PI * 2);
  ctx.fillStyle = color;
  ctx.fill();
  ctx.strokeStyle = "#fff";
  ctx.lineWidth = 1.5;
  ctx.stroke();
}

function clear(ctx, w, h, bg = "#fbfdff") {
  ctx.clearRect(0, 0, w, h);
  ctx.fillStyle = bg;
  ctx.fillRect(0, 0, w, h);
}

function drawMainStage() {
  const { ctx, w, h } = setupCanvas(els.main);
  clear(ctx, w, h, "#f8fbfd");

  const grid = 34;
  ctx.strokeStyle = "rgba(0,139,139,0.08)";
  ctx.lineWidth = 1;
  for (let x = 0; x < w; x += grid) {
    ctx.beginPath();
    ctx.moveTo(x, 0);
    ctx.lineTo(x, h);
    ctx.stroke();
  }
  for (let y = 0; y < h; y += grid) {
    ctx.beginPath();
    ctx.moveTo(0, y);
    ctx.lineTo(w, y);
    ctx.stroke();
  }

  const text = "TMP";
  const fontSize = clamp(w * 0.2, 92, 158);
  const cx = w * 0.47;
  const cy = h * 0.54;
  const depthPx = state.depth * 82;
  const dx = depthPx;
  const dy = -depthPx * 0.48;
  const layers = 26;

  ctx.save();
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.font = `900 ${fontSize}px Arial, Helvetica, sans-serif`;

  if (state.stage >= 1) {
    for (let i = layers; i >= 1; i -= 1) {
      const t = i / layers;
      ctx.fillStyle = `rgba(${Math.round(28 + 30 * t)}, ${Math.round(104 + 50 * t)}, ${Math.round(118 + 70 * t)}, ${0.55 + 0.3 * t})`;
      ctx.fillText(text, cx + dx * t, cy + dy * t);
    }
  }

  if (state.stage >= 3) {
    ctx.shadowColor = "rgba(244,163,33,0.35)";
    ctx.shadowBlur = 24;
  }

  ctx.lineJoin = "round";
  ctx.lineWidth = Math.max(2, state.outline * 58);
  ctx.strokeStyle = state.stage >= 3 ? "#111318" : "rgba(32,33,36,0.34)";
  ctx.strokeText(text, cx, cy);
  const grad = ctx.createLinearGradient(cx - 220, cy - 120, cx + 260, cy + 120);
  grad.addColorStop(0, "#ffffff");
  grad.addColorStop(0.24, "#47c3b5");
  grad.addColorStop(0.55, "#f4a321");
  grad.addColorStop(1, "#ff5d5d");
  ctx.fillStyle = grad;
  ctx.fillText(text, cx, cy);
  ctx.restore();

  const metricsCtx = ctx;
  metricsCtx.save();
  metricsCtx.font = `900 ${fontSize}px Arial, Helvetica, sans-serif`;
  const totalWidth = metricsCtx.measureText(text).width;
  let x = cx - totalWidth / 2;
  const rects = [];
  for (const char of text) {
    const cw = metricsCtx.measureText(char).width;
    rects.push({ x, y: cy - fontSize * 0.52, w: cw, h: fontSize * 0.96, char });
    x += cw;
  }
  metricsCtx.restore();

  if (state.stage === 0) {
    ctx.save();
    const compactLabels = document.documentElement.clientWidth <= 720;
    ctx.strokeStyle = "#008b8b";
    ctx.lineWidth = 2;
    ctx.setLineDash([8, 8]);
    rects.forEach((r, i) => {
      ctx.strokeRect(r.x + 3, r.y, r.w - 6, r.h);
      drawDot(ctx, r.x + 3, r.y, "#008b8b");
      drawDot(ctx, r.x + r.w - 3, r.y, "#008b8b");
      drawDot(ctx, r.x + 3, r.y + r.h, "#008b8b");
      drawDot(ctx, r.x + r.w - 3, r.y + r.h, "#008b8b");
      label(ctx, compactLabels ? `q${i + 1}: 4v` : `quad ${i + 1}: 4 vertices`, r.x + 8, r.y - 10, "#008b8b");
    });
    ctx.restore();
  }

  if (state.stage === 1) {
    ctx.save();
    ctx.strokeStyle = "#f4a321";
    ctx.lineWidth = 2;
    rects.forEach((r) => {
      const ox = dx;
      const oy = dy;
      ctx.beginPath();
      ctx.moveTo(r.x, r.y);
      ctx.lineTo(r.x + ox, r.y + oy);
      ctx.lineTo(r.x + r.w + ox, r.y + oy);
      ctx.lineTo(r.x + r.w, r.y);
      ctx.moveTo(r.x + r.w, r.y + r.h);
      ctx.lineTo(r.x + r.w + ox, r.y + r.h + oy);
      ctx.lineTo(r.x + ox, r.y + r.h + oy);
      ctx.lineTo(r.x, r.y + r.h);
      ctx.stroke();
      drawDot(ctx, r.x + ox, r.y + oy, "#f4a321");
      drawDot(ctx, r.x + r.w + ox, r.y + oy, "#f4a321");
      drawDot(ctx, r.x + ox, r.y + r.h + oy, "#f4a321");
      drawDot(ctx, r.x + r.w + ox, r.y + r.h + oy, "#f4a321");
    });
    label(ctx, "compute output: 48 vertices / 108 indices", 26, 42, "#8a5b00");
    ctx.restore();
  }

  if (state.stage === 2) {
    const rayY = cy - fontSize * 0.04 + Math.sin(state.time * 0.002) * 14;
    const startX = w * 0.07;
    const endX = w * 0.93;
    arrow(ctx, startX, rayY + 52, endX, rayY - 64, "#ff5d5d");
    const dots = Math.min(state.raySteps, 38);
    for (let i = 0; i < dots; i += 1) {
      const t = i / (dots - 1);
      const px = lerp(startX, endX, t);
      const py = lerp(rayY + 52, rayY - 64, t);
      drawDot(ctx, px, py, t > 0.52 && t < 0.7 ? "#ff5d5d" : "#6b63ff", t > 0.52 && t < 0.7 ? 5 : 3);
    }
    label(ctx, "raymarch samples SDF", w * 0.08, rayY + 34, "#ff5d5d");
  }

  if (state.stage === 3) {
    ctx.save();
    ctx.globalCompositeOperation = "screen";
    const sparkCount = 16;
    for (let i = 0; i < sparkCount; i += 1) {
      const a = state.time * 0.001 + i * 0.92;
      const px = cx - 170 + (i % 8) * 54 + Math.sin(a) * 10;
      const py = cy - 84 + Math.floor(i / 8) * 110 + Math.cos(a) * 12;
      ctx.strokeStyle = i % 2 ? "#ffffff" : "#f4a321";
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(px - 10, py);
      ctx.lineTo(px + 10, py);
      ctx.moveTo(px, py - 10);
      ctx.lineTo(px, py + 10);
      ctx.stroke();
    }
    ctx.restore();
    label(ctx, "Depth color + outline + bevel light", 26, 42, "#6b3d00");
  }

  const narrowViewport = document.documentElement.clientWidth <= 720;
  const meterX = narrowViewport ? 18 : w - 156;
  const meterY = narrowViewport ? 34 : 42;
  label(ctx, `Depth ${state.depth.toFixed(2)}`, meterX, meterY, "#008b8b");
  label(ctx, `Outline ${state.outline.toFixed(2)}`, meterX, meterY + 36, "#202124");
}

function drawPrism(ctx, x, y, width, height, depth, color) {
  const ox = depth;
  const oy = -depth * 0.52;
  ctx.save();
  ctx.lineJoin = "round";
  ctx.fillStyle = "rgba(0,139,139,0.16)";
  ctx.beginPath();
  ctx.moveTo(x + ox, y + oy);
  ctx.lineTo(x + width + ox, y + oy);
  ctx.lineTo(x + width + ox, y + height + oy);
  ctx.lineTo(x + ox, y + height + oy);
  ctx.closePath();
  ctx.fill();
  ctx.fillStyle = "rgba(244,163,33,0.28)";
  ctx.beginPath();
  ctx.moveTo(x + width, y);
  ctx.lineTo(x + width + ox, y + oy);
  ctx.lineTo(x + width + ox, y + height + oy);
  ctx.lineTo(x + width, y + height);
  ctx.closePath();
  ctx.fill();
  ctx.fillStyle = "rgba(255,93,93,0.18)";
  ctx.beginPath();
  ctx.moveTo(x, y);
  ctx.lineTo(x + ox, y + oy);
  ctx.lineTo(x + width + ox, y + oy);
  ctx.lineTo(x + width, y);
  ctx.closePath();
  ctx.fill();
  ctx.fillStyle = color;
  roundedRect(ctx, x, y, width, height, 4);
  ctx.fill();
  ctx.strokeStyle = "#202124";
  ctx.lineWidth = 2;
  ctx.stroke();
  ctx.beginPath();
  ctx.moveTo(x, y);
  ctx.lineTo(x + ox, y + oy);
  ctx.moveTo(x + width, y);
  ctx.lineTo(x + width + ox, y + oy);
  ctx.moveTo(x + width, y + height);
  ctx.lineTo(x + width + ox, y + height + oy);
  ctx.moveTo(x, y + height);
  ctx.lineTo(x + ox, y + height + oy);
  ctx.stroke();
  ctx.restore();
}

function drawQuadStage() {
  const { ctx, w, h } = setupCanvas(els.quad);
  clear(ctx, w, h);
  const pad = Math.min(w, h) * 0.08;
  const qx = pad;
  const qy = h * 0.28;
  const qw = w * 0.26;
  const qh = h * 0.36;
  const depth = state.depth * Math.min(w, h) * 0.12;

  ctx.save();
  ctx.fillStyle = "#eef9f7";
  roundedRect(ctx, qx, qy, qw, qh, 4);
  ctx.fill();
  ctx.strokeStyle = "#008b8b";
  ctx.lineWidth = 3;
  ctx.stroke();
  ctx.setLineDash([8, 6]);
  ctx.beginPath();
  ctx.moveTo(qx, qy);
  ctx.lineTo(qx + qw, qy + qh);
  ctx.moveTo(qx + qw, qy);
  ctx.lineTo(qx, qy + qh);
  ctx.stroke();
  ctx.setLineDash([]);
  const vertices = [
    [qx, qy, "v0"],
    [qx, qy + qh, "v1"],
    [qx + qw, qy + qh, "v2"],
    [qx + qw, qy, "v3"]
  ];
  vertices.forEach(([vx, vy, name]) => {
    drawDot(ctx, vx, vy, "#008b8b", 5);
    label(ctx, name, vx + 8, vy - 8, "#008b8b");
  });
  ctx.fillStyle = "#202124";
  ctx.font = "800 21px system-ui, sans-serif";
  ctx.fillText("TMP quad", qx, qy - 34);
  ctx.fillStyle = "#5e6470";
  ctx.font = "700 13px system-ui, sans-serif";
  ctx.fillText("position + uv + color + depth", qx, qy + qh + 42);
  ctx.restore();

  arrow(ctx, qx + qw + 48, qy + qh * 0.5, w * 0.56, qy + qh * 0.5, "#202124");
  label(ctx, "CSMain: 1 thread = 1 quad", w * 0.38, qy + qh * 0.5 - 14, "#202124");

  const px = w * 0.62;
  const py = h * 0.26;
  drawPrism(ctx, px, py, w * 0.23, h * 0.34, depth, "#ffffff");

  ctx.save();
  ctx.fillStyle = "#202124";
  ctx.font = "800 21px system-ui, sans-serif";
  ctx.fillText("Extruded draw volume", px, py - 34);
  ctx.font = "800 48px Arial, sans-serif";
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.fillStyle = "#ff5d5d";
  ctx.fillText("T", px + w * 0.115, py + h * 0.17);
  ctx.restore();

  const badgeY = h - 76;
  [
    ["4", "input vertices", "#008b8b"],
    ["16", "output vertices", "#f4a321"],
    ["36", "indices", "#ff5d5d"]
  ].forEach((item, i) => {
    const bx = pad + i * (w * 0.24);
    ctx.save();
    roundedRect(ctx, bx, badgeY, w * 0.2, 48, 7);
    ctx.fillStyle = "#fff";
    ctx.fill();
    ctx.strokeStyle = item[2];
    ctx.lineWidth = 2;
    ctx.stroke();
    ctx.fillStyle = item[2];
    ctx.font = "900 22px ui-monospace, monospace";
    ctx.fillText(item[0], bx + 16, badgeY + 31);
    ctx.fillStyle = "#5e6470";
    ctx.font = "700 13px system-ui, sans-serif";
    ctx.fillText(item[1], bx + 58, badgeY + 30);
    ctx.restore();
  });
}

function drawSdfIntroStage() {
  if (!els.sdfIntro) return;
  const { ctx, w, h } = setupCanvas(els.sdfIntro);
  clear(ctx, w, h);

  if (w < 520) {
    drawSdfIntroStacked(ctx, w, h);
    return;
  }

  const gap = w * 0.045;
  const panelW = (w - gap * 4) / 3;
  const panelH = h * 0.62;
  const top = h * 0.2;
  const panels = [
    { x: gap, title: "1. SDF atlas", accent: "#6b63ff" },
    { x: gap * 2 + panelW, title: "2. threshold", accent: "#f4a321" },
    { x: gap * 3 + panelW * 2, title: "3. rendered text", accent: "#008b8b" }
  ];

  panels.forEach((panel) => {
    roundedRect(ctx, panel.x, top, panelW, panelH, 8);
    ctx.fillStyle = "#ffffff";
    ctx.fill();
    ctx.strokeStyle = "rgba(32,33,36,0.14)";
    ctx.lineWidth = 1.5;
    ctx.stroke();
    ctx.fillStyle = "#202124";
    ctx.font = "800 18px system-ui, sans-serif";
    ctx.fillText(panel.title, panel.x + 16, top + 32);
  });

  drawSdfTexturePanel(ctx, panels[0].x, top, panelW, panelH);
  drawThresholdPanel(ctx, panels[1].x, top, panelW, panelH);
  drawRenderedSdfPanel(ctx, panels[2].x, top, panelW, panelH);

  arrow(ctx, panels[0].x + panelW + gap * 0.18, top + panelH * 0.5, panels[1].x - gap * 0.18, top + panelH * 0.5, "#202124");
  arrow(ctx, panels[1].x + panelW + gap * 0.18, top + panelH * 0.5, panels[2].x - gap * 0.18, top + panelH * 0.5, "#202124");

  const noteY = top + panelH + 48;
  label(ctx, "ぼけているのは距離が滑らかに入っているから", gap, noteY, "#6b63ff");
  label(ctx, "0.5付近を輪郭として切り直す", w * 0.38, noteY, "#8a5b00");
  label(ctx, "画面サイズに合わせて滑らかに補間", w * 0.68, noteY, "#008b8b");
}

function drawSdfIntroStacked(ctx, w, h) {
  const gap = 12;
  const panelX = gap;
  const panelW = w - gap * 2;
  const panelH = (h - gap * 4) / 3;
  const rows = [
    {
      title: "1. SDF atlas",
      accent: "#6b63ff",
      strong: "白黒は距離",
      detail: "ぼけは距離の勾配",
      kind: "texture"
    },
    {
      title: "2. threshold",
      accent: "#f4a321",
      strong: "0.5付近で内外を判定",
      detail: "輪郭を毎回切り直す",
      kind: "threshold"
    },
    {
      title: "3. rendered text",
      accent: "#008b8b",
      strong: "輪郭を再計算",
      detail: "拡大してもシャープ",
      kind: "rendered"
    }
  ];

  rows.forEach((row, index) => {
    const y = gap + index * (panelH + gap);
    roundedRect(ctx, panelX, y, panelW, panelH, 8);
    ctx.fillStyle = "#ffffff";
    ctx.fill();
    ctx.strokeStyle = "rgba(32,33,36,0.14)";
    ctx.lineWidth = 1.5;
    ctx.stroke();

    ctx.fillStyle = "#202124";
    ctx.font = "800 15px system-ui, sans-serif";
    ctx.fillText(row.title, panelX + 14, y + 26);

    const iconSize = Math.min(76, panelH - 50);
    const iconX = panelX + 16;
    const iconY = y + 40;
    if (row.kind === "texture") {
      drawSdfTextureIcon(ctx, iconX, iconY, iconSize);
    } else if (row.kind === "threshold") {
      drawThresholdIcon(ctx, iconX, iconY + 12, Math.min(110, panelW * 0.34), 24);
    } else {
      const scale = iconSize / 150;
      drawMiniT(ctx, iconX + iconSize * 0.5, iconY + iconSize * 0.55, scale, "#008b8b");
    }

    const textX = iconX + iconSize + 18;
    ctx.fillStyle = row.accent;
    ctx.font = "800 14px system-ui, sans-serif";
    ctx.fillText(row.strong, textX, y + 66);
    ctx.fillStyle = "#5e6470";
    ctx.font = "700 12px system-ui, sans-serif";
    ctx.fillText(row.detail, textX, y + 88);

    if (index < rows.length - 1) {
      arrow(ctx, w * 0.5, y + panelH + 2, w * 0.5, y + panelH + gap - 2, "#202124");
    }
  });
}

function drawSdfTextureIcon(ctx, x, y, size) {
  const cells = Math.max(24, Math.round(size));
  const img = ctx.createImageData(cells, cells);
  for (let py = 0; py < cells; py += 1) {
    for (let px = 0; px < cells; px += 1) {
      const nx = (px / (cells - 1)) * 2 - 1;
      const ny = (py / (cells - 1)) * 2 - 1;
      const d = sdfDistanceToT(nx, ny);
      const v = clamp(0.5 - d / 0.42, 0, 1);
      const shade = Math.round(v * 255);
      const i = (py * cells + px) * 4;
      img.data[i] = shade;
      img.data[i + 1] = shade;
      img.data[i + 2] = shade;
      img.data[i + 3] = 255;
    }
  }
  drawImageDataAt(ctx, img, x, y);
  ctx.strokeStyle = "#202124";
  ctx.lineWidth = 1.5;
  ctx.strokeRect(x, y, cells, cells);
}

function drawThresholdIcon(ctx, x, y, w, h) {
  const gradient = ctx.createLinearGradient(x, 0, x + w, 0);
  gradient.addColorStop(0, "#111");
  gradient.addColorStop(0.5, "#888");
  gradient.addColorStop(1, "#fff");
  roundedRect(ctx, x, y, w, h, 6);
  ctx.fillStyle = gradient;
  ctx.fill();
  ctx.strokeStyle = "#202124";
  ctx.lineWidth = 1.4;
  ctx.stroke();

  const edgeX = x + w * 0.5;
  ctx.strokeStyle = "#ff5d5d";
  ctx.lineWidth = 2.5;
  ctx.beginPath();
  ctx.moveTo(edgeX, y - 12);
  ctx.lineTo(edgeX, y + h + 12);
  ctx.stroke();
  ctx.fillStyle = "#ff5d5d";
  ctx.font = "800 11px system-ui, sans-serif";
  ctx.fillText("edge", edgeX - 13, y - 16);
}

function drawSdfTexturePanel(ctx, x, y, w, h) {
  const size = Math.min(w * 0.62, h * 0.52);
  const sx = x + (w - size) * 0.5;
  const sy = y + h * 0.25;
  const cells = Math.round(size);
  const img = ctx.createImageData(cells, cells);
  for (let py = 0; py < cells; py += 1) {
    for (let px = 0; px < cells; px += 1) {
      const nx = (px / (cells - 1)) * 2 - 1;
      const ny = (py / (cells - 1)) * 2 - 1;
      const d = sdfDistanceToT(nx, ny);
      const v = clamp(0.5 - d / 0.42, 0, 1);
      const shade = Math.round(v * 255);
      const i = (py * cells + px) * 4;
      img.data[i] = shade;
      img.data[i + 1] = shade;
      img.data[i + 2] = shade;
      img.data[i + 3] = 255;
    }
  }
  drawImageDataAt(ctx, img, sx, sy);
  ctx.strokeStyle = "#202124";
  ctx.lineWidth = 2;
  ctx.strokeRect(sx, sy, cells, cells);
  ctx.fillStyle = "#5e6470";
  ctx.font = "700 13px system-ui, sans-serif";
  ctx.fillText("見た目はぼんやり", x + 18, y + h - 38);
  ctx.fillText("でも中身は距離情報", x + 18, y + h - 18);
}

function drawThresholdPanel(ctx, x, y, w, h) {
  const graphX = x + w * 0.14;
  const graphY = y + h * 0.26;
  const graphW = w * 0.72;
  const graphH = h * 0.48;
  ctx.save();
  ctx.strokeStyle = "rgba(32,33,36,0.14)";
  ctx.lineWidth = 1;
  for (let i = 0; i <= 4; i += 1) {
    const gy = graphY + graphH * (i / 4);
    ctx.beginPath();
    ctx.moveTo(graphX, gy);
    ctx.lineTo(graphX + graphW, gy);
    ctx.stroke();
  }

  const gradient = ctx.createLinearGradient(graphX, 0, graphX + graphW, 0);
  gradient.addColorStop(0, "#111");
  gradient.addColorStop(0.5, "#888");
  gradient.addColorStop(1, "#fff");
  roundedRect(ctx, graphX, graphY + graphH * 0.36, graphW, 34, 8);
  ctx.fillStyle = gradient;
  ctx.fill();
  ctx.strokeStyle = "#202124";
  ctx.stroke();

  const edgeX = graphX + graphW * 0.5;
  ctx.strokeStyle = "#ff5d5d";
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(edgeX, graphY + 10);
  ctx.lineTo(edgeX, graphY + graphH - 4);
  ctx.stroke();
  label(ctx, "edge 0.5", edgeX - 38, graphY + 8, "#ff5d5d");

  ctx.fillStyle = "#202124";
  ctx.font = "800 24px system-ui, sans-serif";
  ctx.fillText("alpha >= edge", graphX, y + h - 54);
  ctx.fillStyle = "#5e6470";
  ctx.font = "700 13px system-ui, sans-serif";
  ctx.fillText("内側/外側をシェーダーで決める", graphX, y + h - 24);
  ctx.restore();
}

function drawRenderedSdfPanel(ctx, x, y, w, h) {
  const cx = x + w * 0.5;
  const cy = y + h * 0.52;
  const scale = Math.min(w, h) / 190;
  ctx.save();
  ctx.shadowColor = "rgba(0,139,139,0.22)";
  ctx.shadowBlur = 18;
  drawMiniT(ctx, cx, cy, scale, "#008b8b");
  ctx.shadowBlur = 0;
  ctx.globalCompositeOperation = "source-over";
  ctx.strokeStyle = "#ffffff";
  ctx.lineWidth = 4;
  ctx.beginPath();
  ctx.moveTo(cx - 62 * scale, cy - 62 * scale);
  ctx.lineTo(cx + 62 * scale, cy - 62 * scale);
  ctx.stroke();
  ctx.fillStyle = "#202124";
  ctx.font = "800 24px system-ui, sans-serif";
  ctx.fillText("crisp edge", x + 18, y + h - 54);
  ctx.fillStyle = "#5e6470";
  ctx.font = "700 13px system-ui, sans-serif";
  ctx.fillText("拡大しても輪郭を再計算できる", x + 18, y + h - 24);
  ctx.restore();
}

function sdfDistanceToT(nx, ny) {
  function sdBox(px, py, cx, cy, hx, hy) {
    const dx = Math.abs(px - cx) - hx;
    const dy = Math.abs(py - cy) - hy;
    return Math.hypot(Math.max(dx, 0), Math.max(dy, 0)) + Math.min(Math.max(dx, dy), 0);
  }
  const top = sdBox(nx, ny, 0, -0.48, 0.68, 0.16);
  const stem = sdBox(nx, ny, 0, 0.12, 0.18, 0.72);
  return Math.min(top, stem);
}

function drawSdfStage() {
  const { ctx, w, h } = setupCanvas(els.sdf);
  clear(ctx, w, h);

  const size = Math.min(w * 0.48, h * 0.72);
  const sx = w * 0.08;
  const sy = h * 0.16;
  const cells = Math.round(size);
  const img = ctx.createImageData(cells, cells);
  for (let y = 0; y < cells; y += 1) {
    for (let x = 0; x < cells; x += 1) {
      const nx = (x / (cells - 1)) * 2 - 1;
      const ny = (y / (cells - 1)) * 2 - 1;
      const d = sdfDistanceToT(nx, ny);
      const v = clamp(0.5 - d / 0.34, 0, 1);
      const shade = Math.round(v * 255);
      const i = (y * cells + x) * 4;
      img.data[i] = shade;
      img.data[i + 1] = shade;
      img.data[i + 2] = shade;
      img.data[i + 3] = 255;
    }
  }
  drawImageDataAt(ctx, img, sx, sy);
  ctx.strokeStyle = "#202124";
  ctx.lineWidth = 2;
  ctx.strokeRect(sx, sy, cells, cells);

  const t = (Math.sin(state.time * 0.001) + 1) * 0.5;
  const sampleX = sx + lerp(cells * 0.18, cells * 0.82, t);
  const sampleY = sy + cells * 0.54 + Math.cos(state.time * 0.0013) * cells * 0.18;
  const nx = ((sampleX - sx) / cells) * 2 - 1;
  const ny = ((sampleY - sy) / cells) * 2 - 1;
  const value = clamp(0.5 - sdfDistanceToT(nx, ny) / 0.34, 0, 1);

  drawDot(ctx, sampleX, sampleY, value >= 0.5 ? "#ff5d5d" : "#6b63ff", 7);
  label(ctx, `sample alpha ${value.toFixed(2)}`, sampleX + 12, sampleY - 12, value >= 0.5 ? "#ff5d5d" : "#6b63ff");

  const barX = w * 0.64;
  const barY = h * 0.24;
  const barW = w * 0.23;
  const barH = 24;
  const grad = ctx.createLinearGradient(barX, 0, barX + barW, 0);
  grad.addColorStop(0, "#111");
  grad.addColorStop(0.5, "#888");
  grad.addColorStop(1, "#fff");
  ctx.fillStyle = grad;
  roundedRect(ctx, barX, barY, barW, barH, 6);
  ctx.fill();
  ctx.strokeStyle = "#202124";
  ctx.stroke();
  const edgeX = barX + barW * 0.5;
  ctx.strokeStyle = "#ff5d5d";
  ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(edgeX, barY - 10);
  ctx.lineTo(edgeX, barY + barH + 10);
  ctx.stroke();
  label(ctx, "edge threshold 0.5", edgeX - 60, barY - 20, "#ff5d5d");

  ctx.fillStyle = "#202124";
  ctx.font = "800 22px system-ui, sans-serif";
  ctx.fillText("SDF atlas", sx, sy - 24);
  ctx.font = "700 14px system-ui, sans-serif";
  ctx.fillStyle = "#5e6470";
  ctx.fillText("UVでサンプルしたalphaを、文字の内外判定に使う", barX, barY + 74);
}

function drawRayStage() {
  const { ctx, w, h } = setupCanvas(els.ray);
  clear(ctx, w, h);

  const boxX = w * 0.32;
  const boxY = h * 0.22;
  const boxW = w * 0.36;
  const boxH = h * 0.48;
  const depth = state.depth * 54;
  drawPrism(ctx, boxX, boxY, boxW, boxH, depth, "rgba(255,255,255,0.92)");

  ctx.save();
  ctx.fillStyle = "rgba(0,139,139,0.16)";
  roundedRect(ctx, boxX + boxW * 0.42, boxY + 20, boxW * 0.16, boxH - 40, 4);
  ctx.fill();
  roundedRect(ctx, boxX + boxW * 0.23, boxY + 20, boxW * 0.54, boxH * 0.16, 4);
  ctx.fill();
  ctx.restore();

  const start = { x: w * 0.08, y: h * 0.75 };
  const end = { x: w * 0.86, y: h * 0.23 };
  arrow(ctx, start.x, start.y, end.x, end.y, "#ff5d5d");
  label(ctx, "camera ray", start.x + 14, start.y - 14, "#ff5d5d");

  const count = state.raySteps;
  let hitDrawn = false;
  for (let i = 0; i < count; i += 1) {
    const t = i / (count - 1);
    const x = lerp(start.x, end.x, t);
    const y = lerp(start.y, end.y, t);
    const insideBox = x > boxX && x < boxX + boxW + depth && y > boxY - depth * 0.52 && y < boxY + boxH;
    const hit = insideBox && t > 0.54 && t < 0.62;
    drawDot(ctx, x, y, hit ? "#ff5d5d" : insideBox ? "#f4a321" : "#6b63ff", hit ? 6 : 3);
    if (hit && !hitDrawn) {
      label(ctx, "SDF hit", x + 12, y - 12, "#ff5d5d");
      hitDrawn = true;
    }
  }

  ctx.fillStyle = "#202124";
  ctx.font = "800 22px system-ui, sans-serif";
  ctx.fillText("Raymarch inside volume", w * 0.08, h * 0.14);
  ctx.fillStyle = "#5e6470";
  ctx.font = "700 14px system-ui, sans-serif";
  ctx.fillText(`${count} steps. Smaller steps make the hit test finer, but cost more GPU work.`, w * 0.08, h * 0.18);
}

function drawMiniT(ctx, cx, cy, scale, fill) {
  ctx.save();
  ctx.fillStyle = fill;
  roundedRect(ctx, cx - 60 * scale, cy - 62 * scale, 120 * scale, 24 * scale, 4);
  ctx.fill();
  roundedRect(ctx, cx - 16 * scale, cy - 42 * scale, 32 * scale, 104 * scale, 4);
  ctx.fill();
  ctx.restore();
}

function drawPolygonMesh(ctx, x, y, w, h) {
  const cx = x + w * 0.48;
  const cy = y + h * 0.5;
  const scale = Math.min(w, h) / 220;
  const depth = Math.min(w, h) * 0.13;

  ctx.save();
  ctx.strokeStyle = "rgba(32,33,36,0.16)";
  ctx.lineWidth = 1;
  for (let i = 0; i < 11; i += 1) {
    const px = x + w * (0.18 + i * 0.06);
    ctx.beginPath();
    ctx.moveTo(px, y + h * 0.2);
    ctx.lineTo(px + w * 0.22, y + h * 0.78);
    ctx.stroke();
  }

  ctx.fillStyle = "rgba(255,93,93,0.24)";
  ctx.beginPath();
  ctx.moveTo(cx - 70 * scale + depth, cy - 64 * scale - depth * 0.42);
  ctx.lineTo(cx + 70 * scale + depth, cy - 64 * scale - depth * 0.42);
  ctx.lineTo(cx + 70 * scale, cy - 64 * scale);
  ctx.lineTo(cx - 70 * scale, cy - 64 * scale);
  ctx.closePath();
  ctx.fill();

  drawMiniT(ctx, cx + depth, cy - depth * 0.42, scale, "rgba(190,54,54,0.38)");
  drawMiniT(ctx, cx, cy, scale, "#ff5d5d");

  ctx.strokeStyle = "#202124";
  ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.moveTo(cx - 70 * scale, cy - 64 * scale);
  ctx.lineTo(cx - 70 * scale + depth, cy - 64 * scale - depth * 0.42);
  ctx.moveTo(cx + 70 * scale, cy - 64 * scale);
  ctx.lineTo(cx + 70 * scale + depth, cy - 64 * scale - depth * 0.42);
  ctx.stroke();

  const points = [
    [cx - 60 * scale, cy - 62 * scale], [cx, cy - 62 * scale], [cx + 60 * scale, cy - 62 * scale],
    [cx - 16 * scale, cy - 20 * scale], [cx + 16 * scale, cy - 20 * scale],
    [cx - 16 * scale, cy + 62 * scale], [cx + 16 * scale, cy + 62 * scale]
  ];
  points.forEach((p, i) => {
    drawDot(ctx, p[0], p[1], i % 2 ? "#f4a321" : "#ff5d5d", 4);
  });

  ctx.fillStyle = "#202124";
  ctx.font = "800 20px system-ui, sans-serif";
  ctx.fillText("Polygon mesh", x + 18, y + 32);
  ctx.fillStyle = "#5e6470";
  ctx.font = "700 13px system-ui, sans-serif";
  ctx.fillText("輪郭を三角形に分割して本物の形状を作る", x + 18, y + h - 22);
  ctx.restore();
}

function drawRaymarchBox(ctx, x, y, w, h) {
  const boxX = x + w * 0.29;
  const boxY = y + h * 0.25;
  const boxW = w * 0.44;
  const boxH = h * 0.43;
  const depth = w * 0.12;
  drawPrism(ctx, boxX, boxY, boxW, boxH, depth, "rgba(255,255,255,0.92)");

  ctx.save();
  drawMiniT(ctx, boxX + boxW * 0.5, boxY + boxH * 0.52, Math.min(w, h) / 260, "rgba(0,139,139,0.22)");

  const start = { x: x + w * 0.12, y: y + h * 0.72 };
  const end = { x: x + w * 0.86, y: y + h * 0.28 };
  arrow(ctx, start.x, start.y, end.x, end.y, "#008b8b");
  for (let i = 0; i < 18; i += 1) {
    const t = i / 17;
    const px = lerp(start.x, end.x, t);
    const py = lerp(start.y, end.y, t);
    const hit = t > 0.52 && t < 0.66;
    drawDot(ctx, px, py, hit ? "#ff5d5d" : "#008b8b", hit ? 5 : 3);
  }
  label(ctx, "SDF samples", x + w * 0.12, y + h * 0.72 - 20, "#008b8b");

  ctx.fillStyle = "#202124";
  ctx.font = "800 20px system-ui, sans-serif";
  ctx.fillText("Raymarch volume", x + 18, y + 32);
  ctx.fillStyle = "#5e6470";
  ctx.font = "700 13px system-ui, sans-serif";
  ctx.fillText("箱の中を探索して、SDFが示す面だけを描く", x + 18, y + h - 22);
  ctx.restore();
}

function drawTradeoffStage() {
  if (!els.tradeoff) return;
  const { ctx, w, h } = setupCanvas(els.tradeoff);
  clear(ctx, w, h);

  const gap = w * 0.04;
  const panelW = (w - gap * 3) / 2;
  const panelH = h * 0.66;
  const top = h * 0.15;
  const leftX = gap;
  const rightX = gap * 2 + panelW;

  [
    [leftX, top, panelW, panelH, "rgba(255,93,93,0.06)"],
    [rightX, top, panelW, panelH, "rgba(0,139,139,0.06)"]
  ].forEach(([x, y, pw, ph, fill]) => {
    roundedRect(ctx, x, y, pw, ph, 8);
    ctx.fillStyle = fill;
    ctx.fill();
    ctx.strokeStyle = "rgba(32,33,36,0.14)";
    ctx.lineWidth = 1.5;
    ctx.stroke();
  });

  drawPolygonMesh(ctx, leftX, top, panelW, panelH);
  drawRaymarchBox(ctx, rightX, top, panelW, panelH);

  const centerY = top + panelH + 56;
  const metrics = [
    ["手軽さ", 0.34, 0.76],
    ["CPU負荷", 0.62, 0.24],
    ["GPU pixel負荷", 0.28, 0.78]
  ];
  metrics.forEach(([name, poly, ray], i) => {
    const y = centerY + i * 28;
    ctx.fillStyle = "#5e6470";
    ctx.font = "700 12px system-ui, sans-serif";
    ctx.fillText(name, gap, y + 4);
    drawMeter(ctx, gap + 110, y - 8, panelW * 0.34, 12, poly, "#ff5d5d");
    drawMeter(ctx, rightX + 110, y - 8, panelW * 0.34, 12, ray, "#008b8b");
  });
}

function drawMeter(ctx, x, y, w, h, amount, color) {
  roundedRect(ctx, x, y, w, h, h / 2);
  ctx.fillStyle = "rgba(32,33,36,0.09)";
  ctx.fill();
  roundedRect(ctx, x, y, w * amount, h, h / 2);
  ctx.fillStyle = color;
  ctx.fill();
}

function updateUI() {
  els.depthValue.textContent = state.depth.toFixed(2);
  els.outlineValue.textContent = state.outline.toFixed(2);
  els.rayValue.textContent = String(state.raySteps);
  els.quadStat.textContent = String(quads);
  els.vertexStat.textContent = String(quads * 16);
  els.indexStat.textContent = String(quads * 36);
  els.stageCaption.textContent = stageCopy[state.stage].caption;
  els.stageText.textContent = stageCopy[state.stage].text;
  document.querySelectorAll(".step-tab").forEach((button) => {
    button.classList.toggle("is-active", Number(button.dataset.stage) === state.stage);
  });
}

function bindControls() {
  document.querySelectorAll(".step-tab").forEach((button) => {
    button.addEventListener("click", () => {
      state.stage = Number(button.dataset.stage);
      updateUI();
      drawMainStage();
    });
  });
  els.depth.addEventListener("input", (event) => {
    state.depth = Number(event.target.value);
    updateUI();
  });
  els.outline.addEventListener("input", (event) => {
    state.outline = Number(event.target.value);
    updateUI();
  });
  els.raySteps.addEventListener("input", (event) => {
    state.raySteps = Number(event.target.value);
    updateUI();
  });
}

function frame(time) {
  state.time = time;
  drawMainStage();
  drawSdfIntroStage();
  drawQuadStage();
  drawSdfStage();
  drawRayStage();
  drawTradeoffStage();
  requestAnimationFrame(frame);
}

bindControls();
updateUI();
requestAnimationFrame(frame);
