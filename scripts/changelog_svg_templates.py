"""Static SVG template strings for gen_changelog_svg.py.

Kept in a separate module to comply with 200-line limit on the generator.
All templates use str.format() placeholders.
"""

DEFS = '''\
  <defs>
    <radialGradient id="bgGlow" cx="50%" cy="50%" r="60%">
      <stop offset="0%" stop-color="#21213f"/>
      <stop offset="55%" stop-color="#1a1a2e"/>
      <stop offset="100%" stop-color="#141426"/>
    </radialGradient>
    <linearGradient id="vignette" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0%" stop-color="#141426" stop-opacity="0.9"/>
      <stop offset="18%" stop-color="#141426" stop-opacity="0"/>
      <stop offset="82%" stop-color="#141426" stop-opacity="0"/>
      <stop offset="100%" stop-color="#141426" stop-opacity="0.9"/>
    </linearGradient>
    <radialGradient id="orbCore" cx="50%" cy="50%" r="50%">
      <stop offset="0%" stop-color="#d8fff0"/>
      <stop offset="35%" stop-color="#3ad29f"/>
      <stop offset="100%" stop-color="#1a1a2e" stop-opacity="0"/>
    </radialGradient>
    <radialGradient id="orbHalo" cx="50%" cy="50%" r="50%">
      <stop offset="0%" stop-color="#3ad29f" stop-opacity="0.55"/>
      <stop offset="60%" stop-color="#3ad29f" stop-opacity="0.12"/>
      <stop offset="100%" stop-color="#3ad29f" stop-opacity="0"/>
    </radialGradient>
    <linearGradient id="scanBeam" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0%" stop-color="#e94560" stop-opacity="0"/>
      <stop offset="40%" stop-color="#e94560" stop-opacity="0"/>
      <stop offset="50%" stop-color="#e94560" stop-opacity="0.22"/>
      <stop offset="60%" stop-color="#e94560" stop-opacity="0"/>
      <stop offset="100%" stop-color="#e94560" stop-opacity="0"/>
    </linearGradient>
    <filter id="soft" x="-60%" y="-60%" width="220%" height="220%">
      <feGaussianBlur stdDeviation="6"/>
    </filter>
    <filter id="softText" x="-30%" y="-60%" width="160%" height="220%">
      <feGaussianBlur stdDeviation="3.2"/>
    </filter>
    <filter id="scanSoft" x="-5%" y="-5%" width="110%" height="110%">
      <feGaussianBlur stdDeviation="0.8"/>
    </filter>
  </defs>'''

GRID = '''\
  <!-- LAYER 1: faint instrument grid -->
  <g stroke="#888899" stroke-opacity="0.06" stroke-width="1">
    <line x1="0" y1="90" x2="{w}" y2="90"/>
    <line x1="0" y1="180" x2="{w}" y2="180"/>
    <line x1="0" y1="270" x2="{w}" y2="270"/>
    <line x1="{w3}" y1="0" x2="{w3}" y2="360"/>
    <line x1="{w2}" y1="0" x2="{w2}" y2="360"/>
    <line x1="{w23}" y1="0" x2="{w23}" y2="360"/>
  </g>'''

SCANLINES = '''\
  <!-- LAYER 2: CRT scanline drift -->
  <g filter="url(#scanSoft)">
    <line x1="0" x2="{w}" y1="0"   y2="0"   stroke="#888899" stroke-width="1" stroke-opacity="0.05"><animateTransform attributeName="transform" type="translate" values="0 0; 0 360" dur="10s" begin="0s"    repeatCount="indefinite"/></line>
    <line x1="0" x2="{w}" y1="30"  y2="30"  stroke="#888899" stroke-width="1" stroke-opacity="0.04"><animateTransform attributeName="transform" type="translate" values="0 0; 0 360" dur="10s" begin="-0.83s" repeatCount="indefinite"/></line>
    <line x1="0" x2="{w}" y1="60"  y2="60"  stroke="#888899" stroke-width="1" stroke-opacity="0.03"><animateTransform attributeName="transform" type="translate" values="0 0; 0 360" dur="10s" begin="-1.67s" repeatCount="indefinite"/></line>
    <line x1="0" x2="{w}" y1="90"  y2="90"  stroke="#888899" stroke-width="1" stroke-opacity="0.05"><animateTransform attributeName="transform" type="translate" values="0 0; 0 360" dur="10s" begin="-2.5s"  repeatCount="indefinite"/></line>
    <line x1="0" x2="{w}" y1="120" y2="120" stroke="#888899" stroke-width="1" stroke-opacity="0.04"><animateTransform attributeName="transform" type="translate" values="0 0; 0 360" dur="10s" begin="-3.33s" repeatCount="indefinite"/></line>
    <line x1="0" x2="{w}" y1="150" y2="150" stroke="#888899" stroke-width="1" stroke-opacity="0.03"><animateTransform attributeName="transform" type="translate" values="0 0; 0 360" dur="10s" begin="-4.17s" repeatCount="indefinite"/></line>
    <line x1="0" x2="{w}" y1="180" y2="180" stroke="#888899" stroke-width="1" stroke-opacity="0.05"><animateTransform attributeName="transform" type="translate" values="0 0; 0 360" dur="10s" begin="-5s"    repeatCount="indefinite"/></line>
    <line x1="0" x2="{w}" y1="210" y2="210" stroke="#888899" stroke-width="1" stroke-opacity="0.04"><animateTransform attributeName="transform" type="translate" values="0 0; 0 360" dur="10s" begin="-5.83s" repeatCount="indefinite"/></line>
    <line x1="0" x2="{w}" y1="240" y2="240" stroke="#888899" stroke-width="1" stroke-opacity="0.03"><animateTransform attributeName="transform" type="translate" values="0 0; 0 360" dur="10s" begin="-6.67s" repeatCount="indefinite"/></line>
    <line x1="0" x2="{w}" y1="270" y2="270" stroke="#888899" stroke-width="1" stroke-opacity="0.05"><animateTransform attributeName="transform" type="translate" values="0 0; 0 360" dur="10s" begin="-7.5s"  repeatCount="indefinite"/></line>
    <line x1="0" x2="{w}" y1="300" y2="300" stroke="#888899" stroke-width="1" stroke-opacity="0.04"><animateTransform attributeName="transform" type="translate" values="0 0; 0 360" dur="10s" begin="-8.33s" repeatCount="indefinite"/></line>
    <line x1="0" x2="{w}" y1="330" y2="330" stroke="#888899" stroke-width="1" stroke-opacity="0.03"><animateTransform attributeName="transform" type="translate" values="0 0; 0 360" dur="10s" begin="-9.17s" repeatCount="indefinite"/></line>
  </g>'''

BASELINE = '''\
  <!-- LAYER 3: ECG baseline at y=240 -->
  <path d="M0 240 L520 240 L548 240 L560 240 L572 214 L584 268 L596 226 L608 240 L640 240 L{wend} 240"
        fill="none" stroke="#3ad29f" stroke-opacity="0.55" stroke-width="2"/>
  <rect x="{wc}" y="231" width="9" height="18" fill="#3ad29f">
    <animate attributeName="opacity" values="0;1;1;0" keyTimes="0;0.1;0.85;1" dur="1.6s" repeatCount="indefinite"/>
  </rect>'''

SCANBEAM = '''\
  <!-- LAYER 6: red scan-beam sweep -->
  <rect x="0" y="0" width="{w}" height="360" fill="url(#scanBeam)">
    <animateTransform attributeName="transform" type="translate" values="-{w} 0; {w} 0" dur="6s" repeatCount="indefinite"/>
  </rect>'''

CAPTION = '''\
  <text x="40" y="40" text-anchor="start" font-size="13" fill="#888899" opacity="0.7"><tspan fill="#3ad29f">$ </tspan>git log --reverse  // data flows in, pulse flows out</text>'''
