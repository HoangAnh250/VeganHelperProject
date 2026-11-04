#!/usr/bin/env python3
"""Create English, ingredient-aware cooking steps for the 200 sample meals."""

import csv
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "docs" / "data" / "meal-plan-seed"

BASES = {
    "rice", "brownrice", "stickyrice", "noodles", "pasta", "oats", "quinoa",
    "millet", "barley", "bulgur", "couscous", "tortilla", "bread", "flour",
    "riceflour",
}
PROTEINS = {
    "tofu", "tempeh", "chickpea", "lentil", "blackbean", "kidney", "pinto",
    "whitebean", "mung", "edamame", "egg", "yogurt", "greek", "cottage",
    "cheddar", "mozzarella",
}
AROMATICS = {"onion", "garlic", "ginger", "lemongrass", "leek"}
SEASONINGS = {
    "salt", "soysauce", "sugar",
    "cumin", "curry", "turmeric", "paprika", "chili", "blackpepper", "cinnamon",
}
FATS = {"sesameoil", "oil", "oliveoil", "butter"}
LIQUIDS = {"water", "soymilk", "milk", "coconut"}
FRUIT = {
    "banana", "apple", "mango", "blueberry", "strawberry", "pineapple", "dates",
    "raisins", "avocado", "lemon", "lime",
}
FRESH_PRODUCE = {
    "mushroom", "carrot", "spinach", "broccoli", "cabbage", "tomato", "cucumber",
    "pepper", "sweetpotato", "potato", "eggplant", "zucchini", "cauliflower",
    "pumpkin", "corn", "lettuce", "asparagus", "peas", "bokchoy", "sprouts",
}
NUTS = {"peanut", "almond", "walnut", "cashew", "peanutbutter"}
FINISHERS = {"salt", "soysauce", "lemon", "lime", "blackpepper", "chili", "basil", "cilantro", "parsley", "mint", "sesame"}


def readable(name: str) -> str:
    for prefix in ("Cooked ", "Raw ", "Dry "):
        if name.startswith(prefix):
            name = name[len(prefix):]
    return name[0].lower() + name[1:] if name else name


def join(items: list[str]) -> str:
    items = [item for item in items if item]
    if not items:
        return "the prepared ingredients"
    if len(items) == 1:
        return items[0]
    if len(items) == 2:
        return f"{items[0]} and {items[1]}"
    return f"{', '.join(items[:-1])}, and {items[-1]}"


def generate(meal: dict, labels: dict[str, str]) -> list[str]:
    title = meal["name"]
    name = title.lower()
    keys = list(meal["recipe"].keys())
    all_items = [readable(labels[key]) for key in keys]
    by_key = {key: readable(labels[key]) for key in keys}
    grain = next((by_key[key] for key in keys if key in BASES), None)
    proteins = [by_key[key] for key in keys if key in PROTEINS]
    produce = [by_key[key] for key in keys if key in FRESH_PRODUCE or key in FRUIT]
    aromatics = [by_key[key] for key in keys if key in AROMATICS]
    seasonings = [by_key[key] for key in keys if key in SEASONINGS]
    fruit = [by_key[key] for key in keys if key in FRUIT]
    prep = f"Set out {join(all_items)}. Cut the fresh produce into bite-size pieces and drain any cooked beans."
    finish = f"Taste and finish with {join(seasonings[-2:])}; serve while warm."

    # Explicit methods for dishes that need more context than a broad
    # ingredient-category template can provide.
    curated = {
        "Vietnamese Sesame Tempeh Noodle Bowl": [
            "Warm the cooked rice noodles according to their package directions, drain well, and set aside.",
            "Pan-sear the cooked tempeh in a lightly oiled skillet until browned on both sides; let it cool slightly, then slice it.",
            "Whisk the lime juice with a spoonful of warm water. Toss the noodles with this dressing, then fold in the carrot and cucumber.",
            "Top with the tempeh and sesame seeds. Serve warm or at room temperature.",
        ],
        "Vietnamese Coconut Sweet Potato Rice Porridge": [
            "Cut the sweet potato into small cubes. Combine it with the water in a saucepan and bring to a gentle simmer.",
            "Cook for 10–15 minutes, stirring occasionally, until the sweet potato is fork-tender.",
            "Stir in the cooked rice, mung beans, and coconut milk. Simmer for 8–10 minutes, stirring and adding a little water if the porridge gets too thick.",
            "Taste, season with the listed salt if available, and serve hot.",
        ],
        "Vietnamese Lime Herb Chickpea Noodles": [
            "Warm the cooked rice noodles according to their package directions, drain well, and let them cool until just warm.",
            "Whisk the lime juice with a spoonful of warm water. Crush the peanuts coarsely and set them aside.",
            "Toss the noodles with the dressing, chickpeas, and cucumber until evenly coated.",
            "Fold in the mint and cilantro, scatter the crushed peanuts over the top, and serve at room temperature.",
        ],
        "Vietnamese Peanut Sauce Noodle Salad": [
            "Warm the cooked rice noodles briefly according to their package directions, drain, rinse with cool water, and drain again.",
            "Whisk the peanut butter with lime juice and enough water to make a pourable dressing.",
            "Toss the noodles with the dressing, tofu, cabbage, and carrot until evenly coated.",
            "Taste and adjust with only the listed ingredients. Serve chilled or at room temperature.",
        ],
        "White Bean Tomato Pasta": [
            "Warm the tomato sauce in a skillet over medium-low heat. Stir in the navy beans and simmer for 4–5 minutes.",
            "Tear or chop the fresh basil and stir half of it into the sauce.",
            "Add the cooked pasta and a spoonful of pasta water; toss until the sauce coats the pasta evenly.",
            "Finish with the olive oil and remaining basil, then serve hot.",
        ],
        "Walnut Basil Chickpea Pasta": [
            "Crush the walnuts finely. Stir them with the olive oil and a spoonful of pasta water to make a loose, coarse sauce.",
            "Warm the chickpeas in a skillet over medium-low heat, then stir in the walnut mixture and lemon juice.",
            "Add the cooked pasta and toss until evenly coated, loosening with more pasta water if needed.",
            "Fold in the fresh basil and serve immediately.",
        ],
        "Pumpkin Rice Risotto": [
            "Finely dice the pumpkin and onion so they cook evenly.",
            "Warm the olive oil in a saucepan and cook the onion for 2–3 minutes. Add the pumpkin and a splash of water; cover and cook until fork-tender.",
            "Stir in the cooked rice and chickpeas. Add small splashes of water and stir until the mixture is creamy and hot.",
            "Taste, season with the listed ingredients, and serve while warm.",
        ],
        "Lemon Asparagus Tempeh Couscous": [
            "Cut the asparagus into short lengths and the cooked tempeh into bite-size pieces.",
            "Warm a little olive oil in a skillet. Cook the asparagus for 4–6 minutes until crisp-tender, then add the tempeh and heat through.",
            "Fluff the cooked couscous with a fork and fold it into the skillet. Add a spoonful of water if it seems dry.",
            "Remove from the heat, stir in the lemon juice, and serve warm.",
        ],
        "Lentil Mozzarella Pasta": [
            "Warm the tomato sauce in a skillet and stir in the cooked lentils. Simmer gently for 4–5 minutes.",
            "Add the cooked pasta and a spoonful of pasta water; toss until the sauce coats it evenly.",
            "Tear the mozzarella into small pieces and scatter it over the pasta. Cover for 1–2 minutes until softened.",
            "Drizzle with olive oil and serve hot.",
        ],
    }
    if title in curated:
        return curated[title]

    if "overnight oats" in name:
        fruit_bits = [by_key[key] for key in keys if key not in {"oats", "soymilk", "chia", "flax", "cinnamon"}]
        seeds = [by_key[key] for key in keys if key in {"chia", "flax", "cinnamon"}]
        return [
            f"Stir the dry rolled oats and soy milk together in a lidded jar until every oat is moistened.",
            f"Fold in {join(fruit_bits + seeds)}.",
            "Cover and refrigerate for at least 6 hours, preferably overnight, until thick and spoonable.",
            "Stir before serving; loosen with a small splash of soy milk if needed and serve chilled.",
        ]
    if "smoothie" in name:
        additions = [by_key[key] for key in keys if key not in {"soymilk", "oats"}]
        return [
            f"Add the soy milk and rolled oats to a blender first, then add {join(additions)}.",
            "Blend until completely smooth, stopping once to scrape down the sides if needed.",
            "Let the smoothie stand for 2 minutes so the oats soften, then blend briefly again.",
            "Pour into a glass and serve immediately; add a little cold water only if it is too thick to drink.",
        ]
    if ("pancake" in name or "pancakes" in name) and "rice pancake" not in name and "crepe" not in name:
        vegan = "flax" in keys
        dry = [by_key[key] for key in keys if key in {"oats", "flour", "bakingpowder"}]
        wet = [by_key[key] for key in keys if key in {"soymilk", "egg", "milk", "flax", "banana", "cottage"}]
        fruit_additions = [by_key[key] for key in keys if key in FRUIT and key not in {"banana"}]
        return [
            f"Whisk {join(dry)} in a bowl. In a second bowl, combine {join(wet)}{'; rest for 5 minutes so the flax thickens' if vegan else ''}.",
            f"Stir the wet mixture into the dry ingredients just until combined.{(' Fold in ' + join(fruit_additions) + '.') if fruit_additions else ''} Do not overmix.",
            "Warm a lightly oiled skillet over medium heat. Pour small rounds of batter and cook until bubbles appear and the edges look set, about 2 minutes.",
            "Flip and cook the other side for 1–2 minutes until golden and cooked through. Serve warm with the fruit from the ingredient list.",
        ]
    if "smoothie" in name:
        return [prep, "Blend the ingredients with the listed milk until smooth.", "Scrape down the blender and blend again briefly to remove any grainy pieces.", "Pour and serve cold."]
    if "toast" in name:
        topping = [by_key[key] for key in keys if key not in {"bread", "lemon", "oil", "salt"}]
        cooking = [by_key[key] for key in keys if key in {"mushroom", "tofu"}]
        steps = ["Toast the bread until crisp at the edges but still tender in the middle."]
        if "egg" in keys:
            steps.append("Heat a little oil in a small skillet and fry the egg until the white is set and the yolk is cooked to your preference.")
        if cooking:
            steps.append(f"Warm a little oil in a skillet and cook {join(cooking)} for 4–6 minutes, until hot and lightly browned.")
        if "avocado" in keys or "chickpea" in keys or "whitebean" in keys:
            spread = [by_key[key] for key in keys if key in {"avocado", "chickpea", "whitebean"}]
            citrus = " with a little lemon juice" if "lemon" in keys else ""
            steps.append(f"Mash {join(spread)}{citrus} until spreadable; leave a few small pieces for texture.")
            steps.append("Spread the mash over the warm toast and arrange the tomato or fruit on top if listed.")
        else:
            soft = [by_key[key] for key in keys if key in {"peanutbutter", "almond", "walnut"}]
            sliced = [by_key[key] for key in keys if key in FRUIT - {"lemon", "lime"} or key in {"tomato"}]
            cooked_topping = [by_key[key] for key in keys if key in {"mushroom", "tofu"}]
            if soft:
                steps.append(f"Spread {join(soft)} over the toast in an even layer.")
                citrus = "Toss sliced fruit with a few drops of lemon juice to slow browning. " if "lemon" in keys and sliced else ""
                steps.append(f"{citrus}Arrange {join(sliced)} over the spread.")
            elif cooked_topping:
                steps.append(f"Pile {join(cooked_topping)} onto the toast and add {join([by_key[k] for k in keys if k in FRUIT or k == 'tomato'])} if listed.")
            else:
                steps.append(f"Arrange {join(topping)} over the toast in an even layer.")
        if "egg" in keys:
            steps.append("Layer the warm vegetables and avocado on the toast, then place the cooked egg on top.")
        steps.append("Season with the listed salt if present and serve straight away, while the bread is still crisp.")
        return steps
    if ("oat" in name or "oats" in name) and any(key in keys for key in {"yogurt", "greek", "cottage"}):
        dairy = [by_key[key] for key in keys if key in {"yogurt", "greek", "cottage"}]
        toppings = [by_key[key] for key in keys if key not in {"oats", "yogurt", "greek", "cottage"}]
        return [
            f"Spoon the rolled oats and {join(dairy)} into a bowl; stir until the oats are evenly coated.",
            f"Slice or prepare {join(toppings)} and fold half into the oat mixture.",
            "Let the bowl stand for 5–10 minutes so the oats soften, or chill it overnight for a thicker texture.",
            "Arrange the remaining fruit, seeds, or nuts on top and serve chilled or at room temperature.",
        ]
    if "pudding" in name or "porridge" in name:
        liquid = [by_key[key] for key in keys if key in LIQUIDS]
        grain_name = grain or "the grain"
        add_ins = [by_key[key] for key in keys if key not in BASES | LIQUIDS | {"salt", "sesameoil", "oil"}]
        vegetables = [by_key[key] for key in keys if key in FRESH_PRODUCE]
        proteins_here = [by_key[key] for key in keys if key in PROTEINS and key != "egg"]
        if "porridge" in name and "oats" in keys:
            return [
                f"Combine the dry rolled oats and {join(liquid)} in a saucepan over medium-low heat.",
                "Simmer for 5–7 minutes, stirring often, until the oats are soft and the porridge is creamy.",
                f"Fold in {join(add_ins)} and cook for another minute, until the apple softens and the raisins plump if included.",
                "Serve warm, with the listed cinnamon stirred through.",
            ]
        if "porridge" in name:
            egg_step = "Beat the egg and drizzle it into the simmering porridge while stirring; cook until fully set." if "egg" in keys else ""
            porridge_finish = [by_key[key] for key in keys if key in {"salt", "sesameoil", "sesame", "cilantro", "peanut", "lime"}]
            porridge_mix = vegetables + proteins_here
            porridge_texture = ("until the vegetables are tender and the protein is heated through" if vegetables and proteins_here
                                else "until the vegetables are tender" if vegetables else "until the protein is heated through")
            porridge_start = (f"Combine the cooked rice, water, and {join([by_key[key] for key in keys if key == 'ginger'])} in a saucepan. Bring to a gentle simmer, stirring to break up the rice."
                              if "ginger" in keys else "Combine the cooked rice and water in a saucepan. Bring to a gentle simmer, stirring to break up the rice.")
            return [
                porridge_start,
                "Simmer for 10–15 minutes until the rice releases starch and the porridge is creamy; add a little water if it thickens too much.",
                f"Stir in {join(porridge_mix)} and cook {porridge_texture}. {egg_step}".strip(),
                f"{('Finish with ' + join(porridge_finish) + ' and ') if porridge_finish else ''}serve hot.",
            ]
        fruit_items = [by_key[key] for key in keys if key in FRUIT and key not in {"lemon", "lime"}]
        pudding_addins = fruit_items + [by_key[key] for key in keys if key in {"chia", "cinnamon", "raisins"}]
        return [
            f"Combine the cooked {grain_name} with {join(liquid)} in a small saucepan over medium-low heat.",
            "Simmer for 8–10 minutes, stirring often, until thick and creamy; add a splash of soy milk if it becomes too dense.",
            f"Fold in {join(pudding_addins)} and warm for 1–2 minutes until the fruit softens and the raisins plump.",
            "Serve warm, or cool and refrigerate for 2 hours for a chilled pudding.",
        ]
    if "omelet" in name:
        vegetables = [by_key[key] for key in keys if key not in {"egg", "cheddar", "mozzarella", "bread", "oil", "butter"}]
        cheese = [by_key[key] for key in keys if key in {"cheddar", "mozzarella"}]
        return [
            f"Beat the eggs until the whites and yolks are fully combined; prepare {join(vegetables)}.",
            f"Cook the vegetables in a lightly oiled nonstick skillet over medium heat until tender, about 3–5 minutes.",
            "Pour in the eggs and tilt the pan so they cover the base. Cook gently, lifting the set edge to let uncooked egg run underneath.",
            f"When the top is nearly set, add {join(cheese)} if present, fold the omelet, and cook until the eggs are fully set. Serve with the bread if listed.",
        ]
    if "overnight" in name:
        return [prep, "Mix the grain with the listed milk until evenly moistened.", "Cover and chill until soft.", "Stir and serve cold."]
    if "salad" in name:
        if "noodle" in name:
            cold_fresh = [by_key[key] for key in keys if key in FRESH_PRODUCE and key not in {"mushroom", "carrot", "broccoli", "bokchoy", "spinach"}]
            dressing_items = [by_key[key] for key in keys if key in {"peanutbutter", "oil", "oliveoil", "lemon", "lime", "soysauce", "cumin"}]
            return [
                "Rinse the cooked noodles under cool water, drain thoroughly, and place them in a serving bowl.",
                f"Whisk {join(dressing_items)} with a spoonful of water until pourable.",
                f"Toss the noodles with {join(proteins)} and {join(cold_fresh)} until evenly coated.",
                "Taste and serve chilled or at room temperature.",
            ]
        fresh = [by_key[key] for key in keys if key in FRESH_PRODUCE or key in {"avocado", "mint", "cilantro", "parsley", "basil"}]
        dressing = [by_key[key] for key in keys if key in {"oil", "oliveoil", "lemon", "lime", "soysauce", "cumin"}]
        if "egg" in keys or "potato" in keys:
            prep_steps = []
            if "potato" in keys:
                prep_steps.append("Simmer the potato in salted water for 12–15 minutes until a knife slides in easily; drain and cool before dicing.")
            if "egg" in keys:
                prep_steps.append("Lower the egg into boiling water and cook for 9–10 minutes until hard-boiled; cool, peel, and quarter it.")
            return prep_steps + [
                f"Whisk {join(dressing)} with a teaspoon of water, then gently toss with {join(fresh + [by_key[key] for key in keys if key in PROTEINS and key != 'egg'])}.",
                "Arrange the egg over the salad and fold once more gently so the pieces stay intact.",
                "Taste, adjust using only the listed seasonings, and serve chilled or at room temperature.",
            ]
        return [
            f"If the grain is not already cooked, prepare it according to its package directions, then cool it until just warm or room temperature.",
            f"Combine the cooked {grain or 'grain'} with {join(proteins)} in a serving bowl; fluff the grain and break up any clumps.",
            f"Whisk {join(dressing)} together with a spoonful of water if needed, then toss with {join(fresh)}.",
            "Taste, adjust the dressing using only the listed seasonings, and serve at room temperature or chilled.",
        ]
    if "curry" in name:
        roots = [by_key[key] for key in keys if key in {"potato", "sweetpotato", "carrot", "pumpkin", "cauliflower", "eggplant"}]
        liquids = [by_key[key] for key in keys if key in {"coconut", "milk", "tomatopaste"}]
        tomatoes = [by_key[key] for key in keys if key in {"tomato", "tomatosauce"}]
        leafy = [by_key[key] for key in keys if key in {"spinach"}]
        egg_prep = ["Hard-boil the egg for 9–10 minutes, cool it, peel it, and set it aside."] if "egg" in keys else []
        dairy = [by_key[key] for key in keys if key in {"yogurt", "cottage", "cheddar", "mozzarella"}]
        curry_proteins = [by_key[key] for key in keys if key in PROTEINS and key not in {"egg", "yogurt", "cottage", "cheddar", "mozzarella"}]
        curry_solids = roots + curry_proteins
        curry_sauce = tomatoes + liquids
        curry_start = (f"Warm the listed oil in a saucepan. Cook {join(aromatics)} with the curry powder for 1–2 minutes, stirring until fragrant."
                       if aromatics else "Warm the listed oil in a saucepan. Stir the curry powder in the oil for 30 seconds until fragrant.")
        curry_add = (f"Add {join(curry_solids)} and stir to coat. " if curry_solids else "")
        curry_liquid = (f"Pour in {join(curry_sauce)} and add a splash of water if needed." if curry_sauce else "Add enough water to make a light sauce.")
        curry_finish = (f"Lower the heat and fold in {join(dairy)}. " if dairy else "")
        curry_greens = (f"Stir in {join(leafy)} for the final 2 minutes. " if leafy else "")
        egg_finish = "Add the peeled egg now. " if "egg" in keys else ""
        return [
            *egg_prep,
            curry_start,
            f"{curry_add}{curry_liquid}",
            f"Cover and simmer gently for 12–18 minutes, stirring occasionally, until the root vegetables are tender. {curry_greens}{egg_finish}".strip(),
            f"{curry_finish}Taste, season with the listed salt or pepper, and serve over the cooked grain.".strip(),
        ]
    if "noodle soup" in name or ("soup" in name and "noodle" in name):
        greens = [by_key[key] for key in keys if key in {"bokchoy", "spinach"}]
        broth = [by_key[key] for key in keys if key in {"water", "coconut", "soysauce"}]
        vegetables = [by_key[key] for key in keys if key in FRESH_PRODUCE and key not in {"bokchoy", "spinach"}]
        protein_items = [by_key[key] for key in keys if key in PROTEINS and key != "egg"]
        egg_line = ["If the soup contains egg, crack it into the simmering broth and cook until fully set."] if "egg" in keys else []
        return [
            (f"Warm the sesame oil in a pot and soften {join(aromatics)} for 1–2 minutes. Add {join(broth)} and bring to a gentle simmer."
             if aromatics else f"Warm the sesame oil in a pot, add {join(broth)}, and bring to a gentle simmer."),
            f"Add {join(protein_items + vegetables)} and simmer for 5–8 minutes, until the vegetables are tender and the protein is heated through.",
            f"Stir in {join(greens)} for the final 1–2 minutes, keeping them bright and just wilted." if greens else "Taste the broth and keep it at a gentle simmer.",
            *egg_line,
            "Divide the cooked noodles between bowls, ladle over the hot broth and toppings, and finish with the listed herbs or sesame oil.",
        ]
    if "fried rice" in name:
        extras = [by_key[key] for key in keys if key not in {"rice", "brownrice", "oil", "soysauce", "salt"}]
        egg_step = ["Beat the egg and scramble it in the hot skillet until fully set; transfer it to a plate."] if "egg" in keys else []
        aromatics_line = f"Cook {join(aromatics)} until fragrant, then " if aromatics else "Add "
        vegetables = [by_key[key] for key in keys if key in FRESH_PRODUCE and key not in {"onion", "garlic", "ginger"}]
        return [
            f"Set out the cooked rice and {join(extras)}; separate any clumps of rice with a fork.",
            *egg_step,
            f"Heat the oil in a wide skillet or wok. {aromatics_line}add {join(vegetables)} and cook for 3–5 minutes, stirring often.",
            f"Add {join([by_key[key] for key in keys if key in PROTEINS and key != 'egg'])} and the cooked rice. Stir-fry until steaming hot and lightly toasted in spots." if any(key in PROTEINS and key != "egg" for key in keys) else "Fold in the cooked rice and stir-fry until steaming hot and lightly toasted in spots.",
            ("Return the egg to the pan and toss until evenly distributed. " if "egg" in keys else "") + "Season with the listed soy sauce, salt, or spices and serve immediately.",
        ]
    if "fried noodle" in name or "fried noodles" in name:
        vegetables = [by_key[key] for key in keys if key in FRESH_PRODUCE]
        return [
            f"Separate the cooked noodles and prepare {join(proteins + vegetables)}; drain excess liquid so the noodles can brown.",
            f"Heat oil in a wide pan and stir-fry {join(aromatics + vegetables)} for 3–4 minutes until crisp-tender.",
            f"Add {join(proteins)} and the noodles. Toss over high heat until everything is hot and evenly coated.",
            f"Season with {join(seasonings)} and serve at once while the noodles are glossy and hot.",
        ]
    if ("taco" in name or "burrito" in name) and "bowl" not in name:
        roots = [by_key[key] for key in keys if key in {"sweetpotato", "pumpkin"}]
        root_step = [f"Cook {join(roots)} in a lightly oiled pan until browned and tender."] if roots else []
        protein = [by_key[key] for key in keys if key in PROTEINS and key not in {"cheddar", "mozzarella", "cottage", "yogurt"}]
        fresh = [by_key[key] for key in keys if key in FRESH_PRODUCE and key not in {"sweetpotato", "pumpkin"}]
        cheese = [by_key[key] for key in keys if key in {"cheddar", "mozzarella", "cottage"}]
        citrus = [by_key[key] for key in keys if key in {"lime", "lemon"}]
        cheese_line = f" Fold in {join(cheese)} off the heat." if cheese else ""
        protein_step = f"Warm {join(protein)} with the listed spices in a skillet.{cheese_line}"
        return [
            f"Warm the tortillas in a dry skillet for about 30 seconds per side, then wrap in a clean towel.",
            *root_step,
            protein_step,
            f"Prepare {join(fresh)}{(', then squeeze over ' + join(citrus)) if citrus else ''}.",
            "Fill the tortillas with the warm beans and vegetables, fold, and serve at once.",
        ]
    if "egg" in keys and "pasta" in name and "bake" not in name:
        vegetables = [by_key[key] for key in keys if key in FRESH_PRODUCE]
        cheese = [by_key[key] for key in keys if key in {"mozzarella", "cheddar", "cottage"}]
        return [
            f"Warm oil in a skillet and cook {join(vegetables + aromatics)} until tender; keep the cooked pasta warm.",
            "Beat the egg in a bowl. Stir in the listed cheese and a spoonful of warm pasta water if available.",
            "Take the skillet off the heat, add the pasta and egg mixture, then stir continuously until the egg is fully set by the residual heat. Return to low heat briefly if needed.",
            "Season with the listed salt or pepper and serve immediately.",
        ]
    if "pasta" in name:
        dairy = [by_key[key] for key in keys if key in {"cheddar", "mozzarella", "cottage", "milk"}]
        vegetables = [by_key[key] for key in keys if key in FRESH_PRODUCE]
        sauce = [by_key[key] for key in keys if key in {"tomatosauce", "tomato", "tomatopaste", "basil", "lemon"}]
        pasta_protein = [by_key[key] for key in keys if key in PROTEINS and key not in {"cheddar", "mozzarella", "cottage", "milk"}]
        pasta_nuts = [by_key[key] for key in keys if key in NUTS]
        aromatic_step = (f"Cook {join(aromatics)} in the oil until softened and fragrant."
                         if aromatics else f"Cook {join(vegetables)} in the oil until tender and lightly browned."
                         if vegetables else "Warm the listed sauce gently in the oil.")
        if "bake" in name:
            return [
                f"Heat the oven to 190°C (375°F). Combine the cooked pasta with {join(sauce + pasta_protein + pasta_nuts + vegetables)} in a small baking dish.",
                f"Stir in {join(dairy)} and a spoonful of water if needed so the pasta is lightly coated, not dry." if dairy else "Add a spoonful of water so the pasta is lightly coated, not dry.",
                "Cover loosely and bake for 15 minutes, then uncover and bake 8–12 minutes more until bubbling and golden at the edges.",
                "Rest for 5 minutes before serving so the sauce settles around the pasta.",
            ]
        if pasta_nuts and not sauce:
            sauce_line = f"Blend {join(pasta_nuts)} with a few spoonfuls of hot pasta water until creamy, then stir the sauce into the vegetables."
        else:
            sauce_line = (f"Stir in {join(sauce + pasta_protein + pasta_nuts)} and a few spoonfuls of pasta water; simmer until glossy."
                          if sauce or pasta_protein or pasta_nuts else "Add a few spoonfuls of pasta water and stir until glossy.")
        dairy_line = (f"Fold in {join(dairy)} over low heat until just warmed through." if dairy else "Keep the heat low and toss until the pasta is hot.")
        finishers = [by_key[key] for key in keys if key in FINISHERS | {"basil", "parsley"}]
        return [
            f"Warm the oil in a skillet. {aromatic_step}",
            sauce_line,
            f"Add the cooked pasta and toss until evenly coated. {dairy_line}",
            f"{('Season with ' + join(finishers) + ' and ') if finishers else 'Taste, then '}serve immediately.",
        ]
    if "soup" in name or "stew" in name or name.endswith(" chili"):
        firm = [by_key[key] for key in keys if key in {"potato", "sweetpotato", "carrot", "pumpkin"}]
        soup_veg = [by_key[key] for key in keys if (key in FRESH_PRODUCE or key == "pineapple")
                    and key not in {"onion", "garlic", "ginger", "leek", "potato", "sweetpotato", "carrot", "pumpkin", "tomato", "spinach", "bokchoy"}]
        soup_greens = [by_key[key] for key in keys if key in {"spinach", "bokchoy"}]
        soup_proteins = [by_key[key] for key in keys if key in PROTEINS and key not in {"egg", "yogurt", "greek", "cottage", "cheddar", "mozzarella"}]
        soup_nuts = [by_key[key] for key in keys if key in {"cashew"}]
        soup_dairy = [by_key[key] for key in keys if key in {"yogurt", "greek", "cottage", "cheddar", "mozzarella"}]
        liquids = [by_key[key] for key in keys if key in {"water", "coconut", "milk"}]
        sauces = [by_key[key] for key in keys if key in {"tomato", "tomatosauce", "tomatopaste"}]
        soup_grain = [by_key[key] for key in keys if key in {"rice", "brownrice", "barley"}]
        soup_fat = next((by_key[key] for key in keys if key in FATS), None)
        soup_spices = [by_key[key] for key in keys if key in SEASONINGS and key not in {"salt", "soysauce"}]
        spice_finish = f" Stir in {join(soup_spices)} for 30 seconds." if soup_spices else ""
        if soup_fat and aromatics:
            soup_start = f"Warm the {soup_fat} in a heavy pot and soften {join(aromatics)} for 2–3 minutes.{spice_finish}"
        elif soup_fat:
            soup_start = f"Warm the {soup_fat} in a heavy pot.{spice_finish}"
        elif aromatics:
            soup_start = f"Add a splash of water to a heavy pot and soften {join(aromatics)} for 2–3 minutes.{spice_finish}"
        else:
            soup_start = "Set a heavy pot over medium heat."
        soup_solids = firm + soup_veg + soup_proteins + soup_nuts + sauces
        soup_liquid = f"Add {join(liquids)}" if liquids else "Add enough water to make a light broth"
        soup_finishers = [by_key[key] for key in keys if key in FINISHERS]
        simmer_line = ("Simmer gently for 12–18 minutes until the raw root vegetables are fork-tender."
                       if firm else "Simmer gently for 6–10 minutes until the vegetables are tender and the flavors come together.")
        return [
            soup_start,
            f"Add {join(soup_solids)}. {soup_liquid}; bring to a gentle boil, then lower the heat.",
            f"{simmer_line}{(' Stir in the cooked ' + join(soup_grain) + ' near the end to heat through.') if soup_grain else ''}",
            f"{('Stir in ' + join(soup_greens) + ' for the final 1–2 minutes. ') if soup_greens else ''}{('Stir in ' + join(soup_dairy) + ' off the heat. ') if soup_dairy else ''}{('Taste and season with ' + join(soup_finishers) + '. ') if soup_finishers else ''}Serve hot with the bread if listed.",
        ]
    if "risotto" in name:
        additions = [by_key[key] for key in keys if key not in BASES | SEASONINGS | FATS | AROMATICS | LIQUIDS]
        liquids = [by_key[key] for key in keys if key in {"water", "milk", "coconut"}]
        risotto_start = (f"Warm the oil or butter in a saucepan and soften {join(aromatics)}. Stir in the cooked grain and toast it gently for 1 minute."
                         if aromatics else "Warm the oil or butter in a saucepan. Stir in the cooked grain and toast it gently for 1 minute.")
        risotto_seasoning = [by_key[key] for key in keys if key in {"salt", "blackpepper", "parsley"}]
        return [
            risotto_start,
            f"Add {join(liquids)} a little at a time, stirring until each addition is absorbed and the grain is creamy." if liquids else "Add small splashes of warm water, stirring until the grain is creamy.",
            f"Fold in {join(additions)} and cook gently until the vegetables are tender and everything is hot.",
            f"{('Season with ' + join(risotto_seasoning) + ' and ') if risotto_seasoning else ''}serve while creamy; loosen with a splash of water if it thickens while standing.",
        ]
    if "crepe" in name or "rice pancake" in name:
        liquid_and_binder = [by_key[key] for key in keys if key in LIQUIDS or key == "egg"]
        filling = [by_key[key] for key in keys if (key in PROTEINS or key in FRESH_PRODUCE)
                   and key not in {"egg", "lettuce", "lime", "lemon", "cilantro", "mint", "parsley"}]
        garnish = [by_key[key] for key in keys if key in {"lettuce", "lime", "lemon", "cilantro", "mint", "parsley", "soysauce"}]
        return [
            f"Whisk the rice flour with {join(liquid_and_binder)} and the listed turmeric until smooth; rest the batter for 10 minutes.",
            f"Cook {join(filling)} in a lightly oiled pan until hot, then transfer the filling to a plate.",
            "Pour a thin layer of batter into the hot skillet and swirl it to cover the base. Cook until the edges lift and the underside is golden.",
            f"Add the filling and {join(garnish)} if included, fold the crepe, and serve immediately." if garnish else "Add the filling, fold the crepe, and serve immediately.",
        ]
    if "patty" in name or "patties" in name:
        binders = [by_key[key] for key in keys if key in {"oats", "quinoa", "rice"}]
        return [
            f"Mash {join(proteins)} in a bowl, leaving some texture. Stir in {join(produce + aromatics + binders)} and the listed spices.",
            "Let the mixture stand for 10 minutes so the oats absorb moisture, then shape it into small, firm patties.",
            "Pan-fry in a thin layer of oil over medium heat for 3–4 minutes per side until browned and hot in the center.",
            "Drain briefly on a plate and serve with the listed lemon or fresh vegetables.",
        ]
    if "loaf" in name:
        return [
            "Heat the oven to 190°C (375°F) and line a small loaf pan with parchment.",
            f"Mash {join(proteins)} coarsely, then mix with {join(produce + aromatics)} and the listed oats, tomato paste, and oil until the mixture holds together.",
            "Press firmly into the pan and bake for 35–45 minutes until set and browned on top; rest for 10 minutes before slicing.",
            "Serve warm, spooning over any tomato paste mixture left in the bowl as a simple glaze.",
        ]
    if "bake" in name:
        grain_items = [by_key[key] for key in keys if key in BASES and key not in {"bread", "tortilla"}]
        dairy = [by_key[key] for key in keys if key in {"cheddar", "mozzarella", "cottage", "milk", "yogurt"}]
        sauce = [by_key[key] for key in keys if key in {"tomato", "tomatosauce", "tomatopaste"}]
        vegetables = [by_key[key] for key in keys if key not in BASES | PROTEINS | SEASONINGS | AROMATICS | LIQUIDS | {"tomato", "tomatosauce", "tomatopaste"}]
        base_line = f"Combine {join(grain_items)} with " if grain_items else "Arrange "
        return [
            "Heat the oven to 190°C (375°F) and lightly oil a small baking dish.",
            f"{base_line}{join(vegetables + proteins + sauce + dairy)} in the dish; spread into an even layer.",
            f"Cover with foil and bake for 20 minutes. Uncover and bake another 10–15 minutes until the vegetables are tender and the top is bubbling and lightly browned.",
            "Rest for 5 minutes before serving so the sauce settles and the bake holds together.",
        ]
    if "couscous" in name and "roast" not in name:
        cook_veg = [by_key[key] for key in keys if key in FRESH_PRODUCE and key not in {"cucumber", "tomato", "avocado", "lettuce"}]
        raw_finish = [by_key[key] for key in keys if key in {"cucumber", "tomato", "avocado", "lettuce", "lemon", "lime", "parsley", "cilantro", "mint"}]
        return [
            f"Fluff the cooked couscous with a fork and warm it gently with a spoonful of water if it has cooled.",
            f"Cook {join(cook_veg)} in a lightly oiled skillet until tender; warm {join(proteins)} through in the same pan. Add the listed spices near the end." if cook_veg else f"Warm {join(proteins)} in a lightly oiled skillet until heated through.",
            f"Fold the warm vegetables and protein into the couscous. Add {join(raw_finish)} at the end so fresh ingredients stay bright." if raw_finish else "Fold the warm vegetables and protein into the couscous.",
            "Taste and serve warm or at room temperature.",
        ]
    if "roast" in name:
        vegetables = [by_key[key] for key in keys if key in FRESH_PRODUCE]
        base = [by_key[key] for key in keys if key in BASES]
        return [
            "Heat the oven to 220°C (425°F) and line a small tray with parchment.",
            f"Toss {join(vegetables)} with the listed oil and spices; spread in one layer and roast for 18–25 minutes, turning once, until browned and tender.",
            f"Warm {join(proteins)} in a skillet with a spoonful of water until hot; fluff the cooked grain if one is listed.",
            f"Spoon {join(base)} into a bowl, top with the roasted vegetables and beans, and finish with the listed lemon or herbs.",
        ]
    if "sticky rice" in name:
        toppings = [by_key[key] for key in keys if key not in {"stickyrice", "coconut", "salt", "sugar"}]
        return [
            "Warm the cooked sticky rice in a steamer or covered pan with a spoonful of water until soft and steaming.",
            f"Warm the coconut gently with the listed salt or sugar; do not let it boil hard.",
            f"Fold {join(toppings)} into the rice, or spoon it over the top if it is a garnish.",
            "Serve warm, finishing with the listed sesame or coconut mixture.",
        ]
    if "shepherd" in name and "pie" in name:
        pie_start = (f"Meanwhile, warm oil in a skillet and cook {join(aromatics)} until softened. Add the lentils, carrot, and peas with a splash of water; simmer until the carrot is tender and the filling is thick."
                     if aromatics else "Meanwhile, warm oil in a skillet. Add the lentils, carrot, and peas with a splash of water; simmer until the carrot is tender and the filling is thick.")
        return [
            "Heat the oven to 200°C (400°F). Peel and cube the potatoes, then simmer them in water until fork-tender, about 12–15 minutes.",
            pie_start,
            "Drain and mash the potatoes until smooth, using a little cooking water and the listed oil to loosen them.",
            "Spread the lentil filling in a small baking dish, cover with mashed potato, and rough up the top with a fork. Bake for 20–25 minutes until golden at the edges.",
        ]
    if "tortilla" in name and "egg" in keys:
        vegetables = [by_key[key] for key in keys if key in FRESH_PRODUCE]
        cheese = [by_key[key] for key in keys if key in {"cheddar", "mozzarella"}]
        return [
            "Warm the tortilla briefly in a dry skillet until pliable; set it aside under a clean towel.",
            f"Cook {join(vegetables)} in a little oil until just wilted, then transfer them to a plate.",
            f"Scramble the egg in the same skillet until fully set. Fold in {join(cheese)} while the egg is still warm.",
            "Fill the tortilla with the egg and vegetables, fold in the sides, and serve warm.",
        ]
    if "rice" in name and "skillet" in name and "egg" not in keys:
        vegetables = [by_key[key] for key in keys if key in FRESH_PRODUCE]
        topping_proteins = [by_key[key] for key in keys if key in PROTEINS and key not in {"cheddar", "mozzarella", "cottage", "yogurt"}]
        cheese = [by_key[key] for key in keys if key in {"cheddar", "mozzarella", "cottage"}]
        aromatics_step = (f"Soften {join(aromatics)} in the oil. Then add {join(vegetables)} and cook until tender-crisp."
                          if aromatics else f"Cook {join(vegetables)} in the oil until tender-crisp.")
        cheese_finish = f" Off the heat, fold in {join(cheese)}." if cheese else ""
        final_season = [by_key[key] for key in keys if key in FINISHERS]
        return [
            f"Warm the cooked {grain or 'rice'} and keep it covered. Dice the vegetables into small, even pieces.",
            f"Heat oil in a skillet. {aromatics_step}",
            f"Stir in {join(topping_proteins)} and the cooked rice; toss until steaming hot.{cheese_finish}",
            f"{('Finish with ' + join(final_season) + ' and ') if final_season else ''}serve hot from the skillet.",
        ]
    if "egg" in name and "salad" not in name:
        vegetables = [by_key[key] for key in keys if key in FRESH_PRODUCE]
        non_egg_protein = [by_key[key] for key in keys if key in PROTEINS and key != "egg"]
        if "noodle" in name and "soup" not in name:
            vegetables = [by_key[key] for key in keys if key in FRESH_PRODUCE]
            protein_line = f"Add {join(non_egg_protein)} and heat through." if non_egg_protein else ""
            return [
                f"Warm sesame oil in a skillet and cook {join(vegetables + aromatics)} for 3–4 minutes until tender.",
                f"Beat the egg and pour it into the pan; stir until fully set. {protein_line}".strip(),
                "Add the cooked noodles and a splash of the listed soy sauce or water; toss until hot and evenly coated.",
                "Taste, finish with the listed sesame seeds or herbs, and serve at once.",
            ]
        if "bowl" in name:
            vegetables = [by_key[key] for key in keys if key in FRESH_PRODUCE]
            roots = [by_key[key] for key in keys if key in {"potato", "sweetpotato"}]
            bowl_protein = [by_key[key] for key in keys if key in {"corn", "bokchoy"}]
            root_step = f"Pan-cook {join(roots)} until browned and tender." if roots else "Prepare the listed vegetables for the bowl."
            egg_finish = "Fry the egg until its white is fully set." 
            return [
                f"Warm the cooked {grain or 'rice'} and keep it covered. {root_step}",
                f"Cook {join(non_egg_protein + bowl_protein)} in a lightly oiled skillet until hot; leave any cucumber or avocado raw for freshness.",
                egg_finish,
                f"Build the bowl with the warm grain, cooked toppings, and {join(vegetables)}. Finish with the listed sesame, soy sauce, or citrus.",
            ]
        if "fried rice" in name:
            return ["Scramble the egg in a hot, lightly oiled pan until just set, then transfer it to a plate.", f"Stir-fry {join(vegetables + aromatics)} until crisp-tender.", f"Add the cooked rice and {join(proteins)}; toss until steaming hot, then return the egg to the pan.", f"Season with {join(seasonings)} and serve immediately."]
        if "skillet" in name:
            roots = [by_key[key] for key in keys if key in {"potato", "sweetpotato"}]
            root_step = f"Add diced {join(roots)} with a spoonful of water, cover, and cook until tender, about 12 minutes." if roots else f"Cook {join(vegetables + aromatics)} until softened."
            return [f"Warm oil in a skillet. {root_step}", f"Stir in {join(non_egg_protein)} and the listed seasonings; warm until hot." if non_egg_protein else "Let the vegetables soften and cook off any excess liquid.", "Make a small space in the pan, add the eggs, and stir gently until fully set.", f"Serve straight from the skillet with the bread or cooked grain listed."]
        return [f"Cook {join(vegetables + aromatics)} in a lightly oiled skillet until tender.", f"Stir in {join(non_egg_protein)} and heat through." if non_egg_protein else "Lower the heat and prepare the eggs in the same skillet.", "Beat the eggs in a small bowl, pour them into the pan, and stir gently until fully set.", f"Serve hot with {join([by_key[k] for k in keys if k in BASES])} and the listed herbs."]
    if "hash" in name or "skillet" in name or "scramble" in name:
        roots = [by_key[key] for key in keys if key in {"potato", "sweetpotato"}]
        others = [by_key[key] for key in keys if key not in BASES | SEASONINGS | FATS | AROMATICS | {"potato", "sweetpotato"}]
        if roots:
            first_step = f"Cut {join(roots)} into small, even pieces. Heat oil in a lidded skillet and cook them with a pinch of salt for 10–14 minutes, turning until tender."
        else:
            first_step = f"Heat oil in a skillet and cook {join(aromatics)} for 1–2 minutes until fragrant."
        second_step = (f"Add {join(aromatics)} and cook until fragrant, then stir in {join(others)}." if roots
                       else f"Add {join(others)} and cook until hot and lightly browned.")
        return [
            first_step,
            second_step,
            "Cook uncovered for 5–8 minutes, turning occasionally, until the vegetables are tender and the protein is hot and lightly browned.",
            f"{('Season with ' + join(seasonings) + ' and ') if seasonings else ''}serve directly from the pan.",
        ]
    if "noodle" in name:
        hot_vegetables = [by_key[key] for key in keys if key in FRESH_PRODUCE and key not in {"cucumber", "tomato", "avocado", "lettuce", "lime", "lemon"}]
        fresh_finish = [by_key[key] for key in keys if key in {"cucumber", "tomato", "avocado", "lettuce", "lime", "lemon", "mint", "cilantro", "basil", "parsley", "sesame", "peanut"}]
        sauce_items = [by_key[key] for key in keys if key in {"soysauce", "peanutbutter", "lime", "lemon", "sesameoil", "sesame", "coconut"}]
        stir_fry = [by_key[key] for key in keys if key in PROTEINS] + hot_vegetables
        return [
            "Separate the cooked noodles and set them beside the prepared toppings.",
            f"Warm a little oil in a skillet and cook {join(aromatics + stir_fry)} until the vegetables are tender and the protein is hot." if stir_fry else "Warm the listed aromatics in a little oil until fragrant.",
            f"Add the noodles and {join(sauce_items)} with a splash of water; toss until evenly coated and hot." if sauce_items else "Add the noodles with a splash of water and toss until evenly coated and hot.",
            f"Fold in {join(fresh_finish)} at the end and serve immediately." if fresh_finish else "Taste, season with the listed ingredients, and serve immediately.",
        ]
    if "rice" in name or "bowl" in name or "pilaf" in name:
        vegetables = [by_key[key] for key in keys if key in FRESH_PRODUCE]
        rice_proteins = [by_key[key] for key in keys if key in PROTEINS and key not in {"yogurt", "greek", "cottage", "cheddar", "mozzarella", "egg"}]
        aromatics_line = (f"Cook {join(aromatics)} until fragrant, then " if aromatics else "")
        sauteables = [by_key[key] for key in keys if key in FRESH_PRODUCE and key not in {"cucumber", "avocado", "lettuce"}]
        dairy_finish = [by_key[key] for key in keys if key in {"yogurt", "greek", "cottage", "cheddar", "mozzarella"}]
        fresh_finish = [by_key[key] for key in keys if key in {"cucumber", "avocado", "lettuce", "cilantro", "basil", "parsley", "mint", "lemon", "lime", "sesame"}]
        sauce = [by_key[key] for key in keys if key in {"coconut", "soysauce", "tomatosauce", "tomatopaste", "curry", "cumin", "turmeric", "chili", "paprika"}]
        roots = [by_key[key] for key in keys if key in {"potato", "sweetpotato", "pumpkin", "carrot"}]
        if "coconut" in keys:
            coconut_toppings = [by_key[key] for key in keys if key in {"coconut", "sesame", "salt"}]
            return [
                f"Warm the cooked {grain or 'rice'} and keep it covered.",
                f"Heat oil in a skillet and cook {join(aromatics + roots)} for 8–12 minutes, stirring until the vegetables are tender.",
                f"Stir in {join(rice_proteins + coconut_toppings)} with a splash of water; simmer until thick and hot.",
                f"Spoon the coconut mixture over the warm {grain or 'rice'} and serve immediately.",
            ]
        cook_then_add = f"cook {join(aromatics)} until fragrant, then add " if aromatics else "add "
        saute_items = join(sauteables + rice_proteins)
        topping_cook = (f"Heat oil in a skillet and {cook_then_add}{saute_items}; cook until the vegetables are tender and the protein is hot."
                       if sauteables and rice_proteins else
                       f"Heat oil in a skillet and {cook_then_add}{saute_items}; cook until tender."
                       if sauteables else
                       f"Warm {join(rice_proteins)} in a lightly oiled skillet until heated through."
                       if rice_proteins else
                       "Warm the listed toppings gently in a lightly oiled skillet.")
        sauces_line = f"Stir in {join(sauce)} and cook for another 1–2 minutes; " if sauce else ""
        seasoning_line = f"season with {join(seasonings)}; " if seasonings else ""
        finish_items = fresh_finish + dairy_finish
        finish_line = f"Add {join(finish_items)} and serve." if finish_items else "Serve hot."
        return [
            f"Warm the cooked {grain or 'rice'} and fluff it with a fork; keep it covered while preparing the toppings.",
            topping_cook,
            f"{sauces_line}{seasoning_line}Add a spoonful of water if the pan becomes dry.",
            f"Spoon the cooked toppings over the warm {grain or 'rice'}. {finish_line}",
        ]
    return [prep, f"Warm the oil and cook {join(aromatics + produce)} until tender.", f"Add {join(proteins)} and the cooked {grain or 'base'}; heat through and season with {join(seasonings)}.", "Taste once more and serve hot."]


def main() -> None:
    labels = {}
    with (DATA / "ingredients.csv").open(encoding="utf-8-sig", newline="") as source:
        for row in csv.DictReader(source):
            labels[row["key"]] = row["name"]
    seed = json.loads((DATA / "seed-data.json").read_text(encoding="utf-8"))
    recipes = []
    for meal in seed["meals"]:
        recipes.append({"meal_name": meal["name"], "steps": generate(meal, labels)})
    if len(recipes) != 200 or len({recipe["meal_name"] for recipe in recipes}) != 200:
        raise SystemExit("Expected exactly 200 unique meals.")
    if any(not 4 <= len(recipe["steps"]) <= 7 for recipe in recipes):
        raise SystemExit("Every meal must have between 4 and 7 steps.")
    payload = {
        "batch": "VH-MEAL-SEED-20261012-v1",
        "language": "en",
        "provenance": "General sample cooking instructions authored from the listed ingredients and dish style; not dietitian-reviewed.",
        "recipes": recipes,
    }
    path = DATA / "cooking-steps.json"
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {len(recipes)} recipes and {sum(len(recipe['steps']) for recipe in recipes)} ordered steps to {path}.")


if __name__ == "__main__":
    main()
