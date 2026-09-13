// @ts-check
const fs = require('node:fs');
const path = require('node:path');
const eslint = require('@eslint/js');
const { defineConfig } = require('eslint/config');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');
const ngrx = require('@ngrx/eslint-plugin');

// Layer bans encode .claude/rules/ui-architecture.md ("Dependency direction").
const rule = 'see .claude/rules/ui-architecture.md';

const formsBan = {
  name: '@angular/forms',
  importNames: ['FormsModule', 'ReactiveFormsModule'],
  message: `Use Signal Forms; ${rule}.`,
};

const folder = (name) => [`@${name}/*`, `**/app/${name}/*`];

// Negate the directory itself: a gitignore matcher cannot re-include a child of an excluded directory.
const except = (name) => [`!@${name}`, `!**/app/${name}`];

const presentational = [
  ...folder('shared/components'),
  ...folder('shared/directives'),
  ...folder('shared/pipes'),
];

const allOfApp = [
  ...folder('pages'),
  ...folder('components'),
  ...folder('state'),
  ...folder('services'),
  ...folder('core'),
  ...folder('shared'),
];

// Flat config replaces a rule's options when blocks overlap, so each layer block repeats the forms ban.
const layer = (files, group) => ({
  files,
  ignores: ['**/*.spec.ts'],
  rules: {
    'no-restricted-imports': [
      'error',
      {
        paths: [formsBan],
        patterns: [{ group, message: `Crosses a layer boundary; ${rule}.` }],
      },
    ],
  },
});

const featuresOf = (kind) => {
  const dir = path.join(__dirname, 'src', 'app', kind);
  return fs.existsSync(dir)
    ? fs
        .readdirSync(dir, { withFileTypes: true })
        .filter((entry) => entry.isDirectory())
        .map((entry) => entry.name)
    : [];
};

module.exports = defineConfig([
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      tseslint.configs.recommended,
      tseslint.configs.stylistic,
      angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/directive-selector': [
        'error',
        {
          type: 'attribute',
          prefix: 'app',
          style: 'camelCase',
        },
      ],
      '@angular-eslint/component-selector': [
        'error',
        {
          type: 'element',
          prefix: 'app',
          style: 'kebab-case',
        },
      ],
      // OnPush is the v22 default and `ng g c` omits the property, so an explicit one is noise.
      '@angular-eslint/prefer-on-push-component-change-detection': [
        'error',
        { allowExplicitOnPush: false },
      ],
      '@angular-eslint/prefer-inject': 'error',
      '@angular-eslint/prefer-signals': 'error',
      '@angular-eslint/prefer-output-emitter-ref': 'error',
      '@angular-eslint/prefer-host-metadata-property': 'error',
    },
  },
  {
    files: ['**/*.ts'],
    extends: [ngrx.configs.signalsTypeChecked],
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: __dirname,
      },
    },
  },
  {
    files: ['**/*.html'],
    extends: [angular.configs.templateRecommended, angular.configs.templateAccessibility],
    rules: {
      '@angular-eslint/template/prefer-control-flow': 'error',
    },
  },
  {
    files: ['src/**/*.ts'],
    rules: {
      'no-restricted-imports': ['error', { paths: [formsBan] }],
    },
  },
  layer(
    ['src/app/shared/models/**/*.ts'],
    ['@angular/*', '@ngrx/*', ...allOfApp, ...except('shared/models')],
  ),
  layer(
    ['src/app/shared/utils/**/*.ts'],
    ['@angular/*', '@ngrx/*', ...allOfApp, ...except('shared/models'), ...except('shared/utils')],
  ),
  layer(
    ['src/app/core/**/*.ts'],
    [
      ...presentational,
      ...folder('services'),
      ...folder('state'),
      ...folder('components'),
      ...folder('pages'),
    ],
  ),
  layer(
    ['src/app/services/**/*.ts'],
    [...presentational, ...folder('state'), ...folder('components'), ...folder('pages')],
  ),
  layer(
    ['src/app/state/**/*.ts'],
    [...presentational, ...folder('components'), ...folder('pages')],
  ),
  layer(
    ['src/app/shared/{components,directives,pipes}/**/*.ts'],
    ['@state/*/*', '**/app/state/*/*', ...folder('components'), ...folder('pages')],
  ),
  ...featuresOf('components').map((feature) =>
    layer(
      [`src/app/components/${feature}/**/*.ts`],
      [...folder('pages'), ...folder('components'), ...except(`components/${feature}`)],
    ),
  ),
  ...featuresOf('pages').map((feature) =>
    layer(
      [`src/app/pages/${feature}/**/*.ts`],
      [...folder('pages'), ...except(`pages/${feature}`)],
    ),
  ),
]);
