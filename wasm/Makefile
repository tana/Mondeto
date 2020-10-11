.PHONY: shooting-game

shooting-game: raygun.wasm

ASFLAGS = --runtime half
%.wasm: %.ts
	npx asc $< $(ASFLAGS) -o $@

raygun.wasm: raygun.ts mondeto.ts