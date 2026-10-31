import js from "@eslint/js";
import globals from "globals";
import tseslint from "typescript-eslint";

export default tseslint.config(
    { ignores: ["node_modules", "results"] },
    js.configs.recommended,
    tseslint.configs.recommended,
    {
        languageOptions: {
            globals: {
                ...globals.node,
                ...globals.es2022,
            },
        },
        rules: {
            "no-console": ["error", { allow: ["error", "warn"] }],
            "@typescript-eslint/explicit-function-return-type": "off",
            "@typescript-eslint/no-explicit-any": "off",
            "@typescript-eslint/no-unused-vars": [
                "error",
                { argsIgnorePattern: "^_", ignoreRestSiblings: true },
            ],
        },
    },
);
