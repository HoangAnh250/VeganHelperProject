import csv, json
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path

root = Path(__file__).resolve().parents[1]
seed = root / "meal-plan-seed"
foods = {r["key"]: r for r in csv.DictReader((seed/"ingredients.csv").open(encoding="utf-8-sig"))}
names = {r["name"].casefold() for r in csv.DictReader((seed/"meals.csv").open(encoding="utf-8-sig"))}
photos = {r["mealName"]: r for r in json.loads((root/"meal-images/curated.json").read_text(encoding="utf-8-sig"))}
# Dish family | allowed slots | ingredients in edible grams | fillings | produce | image anchor | original instructions.
families = """
Vietnamese Lemongrass Rice Skillet|LD|rice:230 lemongrass:12 garlic:5 soysauce:12 oil:12 lime:10|tofu tempeh edamame mung|bokchoy cabbage broccoli greenbean carrot|Vietnamese Cabbage Mushroom Rice Skillet|Mince the lemongrass and garlic; fry in oil for 30 seconds.|Stir-fry {p} and {v} until hot and tender.|Fold in rice and soy sauce and stir until steaming.|Finish with lime.
Vietnamese Ginger Brown Rice Bowl|LD|brownrice:230 ginger:12 garlic:5 sesameoil:12 soysauce:12 lime:10|tofu tempeh edamame chickpea|bokchoy broccoli carrot greenbean cabbage|Thai-Inspired Basil Tofu Rice|Warm the cooked brown rice.|Slice or drain {p} and cut {v} into bite-size pieces.|Fry ginger and garlic in sesame oil; stir-fry the filling and vegetable until hot and tender.|Serve over rice with soy sauce and lime.
Vietnamese Pineapple Sour Rice Noodle Soup|BLD|noodles:250 water:320 pineapple:65 tomato:75 lime:15 soysauce:12 oil:8 cilantro:8|tofu tempeh edamame mung|bokchoy cabbage sprouts carrot greenbean|Vietnamese Mushroom Rice Noodle Soup|Sauté chopped tomato and pineapple in oil for three minutes.|Add water and soy sauce and simmer for eight minutes.|Add {p} and {v} and simmer until hot and tender.|Warm the noodles in the broth and finish with lime and cilantro.
Vietnamese Garlic and Leek Rice Noodles|BLD|noodles:260 leek:25 garlic:8 oil:13 soysauce:12 lime:10|tofu tempeh edamame mung|sprouts carrot cabbage bokchoy greenbean|Thai-Inspired Peanut Rice Noodles|Slice the leek and mince the garlic.|Stir-fry {p} and {v} in the oil until hot and tender.|Add leek, garlic and cooked noodles; toss with soy sauce over high heat.|Finish with lime and serve hot.
Vietnamese Coconut Sticky Rice|B|stickyrice:190 coconut:45 sesame:12 salt:1|mung blackbean edamame peanut|pumpkin sweetpotato corn carrot banana|Vietnamese Pumpkin Mung Bean Rice Porridge|Steam or reheat the sticky rice until hot.|Cook {v} until tender, or slice it fresh if using banana.|Warm {p} separately if cooked, or toast it if using peanuts.|Combine with coconut and salt and scatter sesame on top.
Vietnamese Ginger Rice Congee|BLD|rice:220 water:300 ginger:12 garlic:5 sesameoil:10 soysauce:10 cilantro:8|mung tofu edamame chickpea|pumpkin carrot spinach bokchoy corn|Vietnamese Pumpkin Mung Bean Rice Porridge|Simmer cooked rice in water with ginger and garlic until creamy.|Dice {v} and cook it in the congee until tender.|Fold in {p} and heat thoroughly.|Season with soy sauce, sesame oil and cilantro.
Vietnamese Savory Coconut Crepes|BLD|riceflour:85 coconut:55 water:130 turmeric:2 oil:12 soysauce:12 lime:12|tofu tempeh mung edamame|sprouts cabbage carrot greenbean bokchoy|Vietnamese Cabbage Mushroom Rice Skillet|Whisk rice flour, coconut, water and turmeric into a thin batter.|Slice or crumble {p}; finely cut {v} and sauté both until hot and tender.|Swirl thin portions of batter in an oiled skillet and cook until crisp around the edges.|Fill and fold the crepes and serve with lime mixed with soy sauce.
Vietnamese Pickled Carrot Sandwich|BL|bread:110 carrot:40 lime:20 sugar:5 cilantro:10 soysauce:12 oil:10|tofu tempeh chickpea egg|cucumber cabbage pepper lettuce sprouts|Avocado Chickpea Toast|Shred carrot, toss with lime and sugar and rest for ten minutes.|Pan-cook {p} with oil and soy sauce; cook egg until set if used.|Slice {v} thinly; cook sprouts first if using them.|Toast and split the bread and add the filling, carrot and cilantro.
Vietnamese Pepper and Ginger Claypot Rice|LD|rice:220 garlic:6 ginger:10 blackpepper:2 cinnamon:1 soysauce:16 sugar:7 oil:10 water:60|tofu tempeh mung chickpea|eggplant greenbean pumpkin carrot cabbage|Vietnamese Chili Garlic Edamame Rice|Sauté garlic, ginger, black pepper and cinnamon in oil.|Add {p} and {v}; stir in soy sauce, sugar and water.|Simmer covered until the vegetable is tender, then reduce the sauce uncovered.|Serve with hot rice.
Vietnamese Coconut Turmeric Rice Curry|LD|rice:190 coconut:90 turmeric:3 curry:5 lemongrass:12 oil:8 water:100 salt:2|tofu tempeh chickpea lentil|pumpkin sweetpotato eggplant potato carrot|Vietnamese Coconut Tofu Curry Rice|Fry minced lemongrass, turmeric and curry powder in oil.|Add diced {v}, coconut, water and salt and simmer until tender.|Add {p} and simmer gently until hot.|Serve over warm rice.
Vietnamese Lime Herb Vermicelli Bowl|LD|noodles:230 lime:22 mint:8 cilantro:8 soysauce:14 sugar:6 oil:12|tofu tempeh chickpea edamame|cucumber cabbage carrot lettuce sprouts|Thai-Inspired Peanut Rice Noodles|Warm cooked noodles briefly, drain and let cool slightly.|Pan-cook {p} in oil until hot and lightly browned.|Slice {v} thinly, cooking it first if using sprouts, and chop the herbs.|Layer the bowl and toss with lime, soy sauce and sugar.
Vietnamese Tomato Braise with Rice|LD|rice:220 tomato:130 garlic:7 oil:12 soysauce:14 sugar:4 cilantro:8|tofu tempeh mung egg|bokchoy greenbean cabbage broccoli eggplant|Vietnamese Cabbage Mushroom Rice Skillet|Cook chopped tomato and garlic in oil until soft.|Stir in soy sauce and sugar and reduce the sauce for three minutes.|Simmer {p} and {v} until hot and tender; cook egg until set if used.|Serve over hot rice with cilantro.
Cinnamon Breakfast Oat Bowl|B|oats:65 water:170 cinnamon:2 salt:1|soymilk milk yogurt greek|banana apple mango blueberry pineapple|Banana Walnut Overnight Oats|Simmer oats, water, cinnamon and salt until thick.|Slice {v} and warm half in the oats.|Stir in {p} off the heat.|Top with the remaining fruit.
Mediterranean Warm Grain and Bean Salad|LD|bulgur:200 lemon:20 parsley:10 oil:14 garlic:5 salt:2|chickpea lentil whitebean kidney|pepper tomato zucchini broccoli eggplant|Mediterranean Chickpea Quinoa Salad|Cut {v} into small pieces and sauté with garlic and half the oil until tender.|Warm cooked bulgur and {p}.|Whisk lemon juice, salt, parsley and remaining oil.|Toss vegetables, grains and beans with the dressing.
Smoky Bean and Vegetable Rice Bowl|LD|brownrice:210 paprika:4 cumin:3 lime:15 oil:13 garlic:6 salt:2|blackbean pinto kidney whitebean|corn pepper zucchini tomato cauliflower|Black Bean Corn Burrito Bowl|Sauté garlic, paprika and cumin in oil until fragrant.|Add {v} and cook until tender.|Stir in {p} and heat thoroughly; season with salt and lime.|Serve over cooked brown rice.
""".strip().splitlines()
slots = dict(B="breakfast", L="lunch", D="dinner")
labels = dict(mung="Mung Bean", blackbean="Black Bean", whitebean="White Bean", kidney="Kidney Bean",
              pinto="Pinto Bean", greenbean="Green Bean", bokchoy="Bok Choy", sweetpotato="Sweet Potato",
              soymilk="Soy Milk", greek="Greek Yogurt", sprouts="Bean Sprout")
meals = []
for line in families:
    family, times, base, fillings, produce, photo, *steps = line.split("|")
    for p in fillings.split():
        for v in produce.split():
            grams = {k: int(g) for k, g in (item.split(":") for item in base.split())}
            grams[p] = grams.get(p, 0) + dict(peanut=35, egg=100, soymilk=180, milk=180, yogurt=170, greek=170).get(p, 120)
            grams[v] = grams.get(v, 0) + 110
            name = f"{family} with {labels.get(p, p.title())} and {labels.get(v, v.title())}"
            assert name.casefold() not in names
            names.add(name.casefold())
            kcal = sum(Decimal(foods[k]["calories_per_100g"]) * g / 100 for k, g in grams.items())
            meals.append(dict(name=name, slots=[slots[t] for t in times], kcal=int(kcal.quantize(Decimal(1), rounding=ROUND_HALF_UP)),
                              ingredients=grams, diet="lacto_ovo_vegetarian" if p in {"egg","milk","yogurt","greek"} else "vegan",
                              photo=photo, photo_source=photos[photo]["sourcePage"],
                              steps=[step.format(p=foods[p]["name"].lower(), v=foods[v]["name"].lower()) for step in steps]))
assert len(meals) == 300
used = {key: foods[key]["name"] for meal in meals for key in meal["ingredients"]}
payload = dict(batch="VH-MEAL-VN-EXPANSION-v1", meals=meals, ingredients=used)
Path(__file__).with_name("expansion-catalog.json").write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
