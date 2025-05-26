# 今は空っぽの function router
function_router: dict[str, callable] = {}

async def handle_function(call: dict):
    name = call.get("name")
    if name in function_router:
        await function_router[name](call.get("arguments", {}))
    # 後日: Graph AI をここで呼ぶ

